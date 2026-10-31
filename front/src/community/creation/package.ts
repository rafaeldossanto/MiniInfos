import { Inflate, zipSync } from 'fflate';

export const MAX_ARCHIVE = 8 * 1024 * 1024;
export const MAX_SOURCE = 64 * 1024 * 1024;
export const MAX_FILE = 32 * 1024 * 1024;
export const states = [
  'idle',
  'active',
  'alert',
  'sleep',
  'protectEyes',
  'heldHead',
  'heldLeftWing',
  'heldRightWing',
  'heldFeet',
  'heldBody',
] as const;
export type Track = {
  row: number;
  frameDurationMs?: number;
  frames?: number[];
  durationsMs?: number[];
  blendMs?: number;
};
export type Manifest = {
  schemaVersion: 1;
  kind: 'theme' | 'mascot';
  id: string;
  name: string;
  author: string;
  version: string;
  license: string;
  description?: string;
  $schema?: string;
  theme?: {
    base?: 'Simple' | 'Light' | 'Classic';
    colors?: Record<string, string>;
    headerImage?: string;
    headerHeight?: number;
    headerTitleSize?: number;
  };
  mascot?: {
    spriteSheet: string;
    columns: number;
    rows: number;
    states: Record<string, Track>;
    anatomy?: Record<string, unknown>;
  };
};
export type PackageFiles = Record<string, Uint8Array>;
export type Creation = { manifest: Manifest; files: PackageFiles; archive: Uint8Array };
export class PackageError extends Error {
  readonly code: string;
  constructor(code: string) {
    super(code);
    this.name = 'PackageError';
    this.code = code;
  }
}
const fail = (code: string): never => {
  throw new PackageError(code);
};
const object = (v: unknown): Record<string, unknown> =>
  v && typeof v === 'object' && !Array.isArray(v)
    ? (v as Record<string, unknown>)
    : fail('manifest');
const keys = (v: Record<string, unknown>, allowed: string[]) => {
  if (Object.keys(v).some((k) => !allowed.includes(k))) fail('fields');
};
const integer = (v: unknown, min: number, max: number) =>
  typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max;
const number = (v: unknown, min: number, max: number) =>
  typeof v === 'number' && Number.isFinite(v) && v >= min && v <= max;
const text = (v: unknown, max: number) =>
  typeof v === 'string' &&
  v.trim().length > 0 &&
  v.length <= max &&
  !/[\u0000-\u001f\u007f]/u.test(v);
const colorKeys = [
  'CardBackground',
  'CardBorder',
  'PrimaryText',
  'DimText',
  'Divider',
  'Track',
  'Hover',
  'Selected',
  'SelectedBorder',
  'IconOther',
  'MenuBackground',
  'MenuBorder',
  'MenuHighlight',
  'MenuDisabled',
  'Accent',
  'AccentText',
  'Warning',
  'Danger',
];

export function packagePath(path: string): string {
  if (!path || path.length > 240 || path.includes('\\')) fail('path');
  const parts = path.split('/');
  if (
    parts.length > 8 ||
    parts.some(
      (p) =>
        !p ||
        p.startsWith('.') ||
        /[. ]$|[:\u0000-\u001f\u007f<>"|?*]/u.test(p) ||
        /^(con|prn|aux|nul|com[1-9]|lpt[1-9]|conin\$|conout\$)(\.|$)/i.test(p),
    )
  )
    fail('path');
  if (!/\.(png|json|md|txt)$/i.test(path)) fail('file-type');
  return path;
}

export function pngSize(data: Uint8Array): [number, number] {
  if (
    data.length < 33 ||
    data.length > MAX_FILE ||
    ![137, 80, 78, 71, 13, 10, 26, 10].every((b, i) => data[i] === b) ||
    new TextDecoder().decode(data.subarray(12, 16)) !== 'IHDR'
  )
    fail('png');
  const v = new DataView(data.buffer, data.byteOffset, data.byteLength);
  const w = v.getUint32(16),
    h = v.getUint32(20);
  if (!w || !h || w > 8192 || h > 8192 || w * h > 16_777_216) fail('image-size');
  return [w, h];
}

/** Inspect ZIP metadata before allocating decompression buffers. ZIP64, links and encrypted entries are unnecessary for a package. */
function inspectZip(bytes: Uint8Array) {
  const v = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  let end = bytes.length - 22;
  while (end >= Math.max(0, bytes.length - 65557) && v.getUint32(end, true) !== 0x06054b50) end--;
  if (
    end < Math.max(0, bytes.length - 65557) ||
    v.getUint16(end + 4, true) ||
    v.getUint16(end + 6, true) ||
    end + 22 + v.getUint16(end + 20, true) !== bytes.length ||
    (end >= 20 && v.getUint32(end - 20, true) === 0x07064b50)
  )
    fail('archive');
  const count = v.getUint16(end + 10, true),
    start = v.getUint32(end + 16, true);
  if (!count || count > 256 || start >= end || v.getUint16(end + 8, true) !== count)
    fail('archive');
  let offset = start,
    total = 0,
    files = 0;
  const seen = new Set<string>();
  const entries: {
    name: string;
    size: number;
    compressed: number;
    data: number;
    method: number;
    crc: number;
    local: number;
    end: number;
  }[] = [];
  const extras = (from: number, length: number) => {
    const stop = from + length;
    while (from < stop) {
      if (from + 4 > stop || v.getUint16(from, true) === 1) fail('archive');
      from += 4 + v.getUint16(from + 2, true);
    }
    if (from !== stop) fail('archive');
  };
  for (let i = 0; i < count; i++) {
    if (offset + 46 > end || v.getUint32(offset, true) !== 0x02014b50) fail('archive');
    const flags = v.getUint16(offset + 8, true),
      method = v.getUint16(offset + 10, true);
    const size = v.getUint32(offset + 24, true),
      nameLength = v.getUint16(offset + 28, true);
    const next =
      offset + 46 + nameLength + v.getUint16(offset + 30, true) + v.getUint16(offset + 32, true);
    if (
      next > end ||
      flags & ~0x080e ||
      v.getUint16(offset + 34, true) !== 0 ||
      ![0, 8].includes(method) ||
      ((v.getUint32(offset + 38, true) >>> 16) & 0xf000) === 0xa000
    )
      fail('archive');
    const name = new TextDecoder('utf-8', { fatal: true }).decode(
      bytes.subarray(offset + 46, offset + 46 + nameLength),
    );
    extras(offset + 46 + nameLength, v.getUint16(offset + 30, true));
    const compressed = v.getUint32(offset + 20, true),
      local = v.getUint32(offset + 42, true);
    if (
      local + 30 > start ||
      v.getUint32(local, true) !== 0x04034b50 ||
      v.getUint16(local + 6, true) !== flags ||
      v.getUint16(local + 8, true) !== method ||
      v.getUint16(local + 26, true) !== nameLength
    )
      fail('archive');
    const data = local + 30 + nameLength + v.getUint16(local + 28, true);
    if (data + compressed > start || compressed === 0xffffffff || size === 0xffffffff)
      fail('archive');
    if (
      !bytes
        .subarray(local + 30, local + 30 + nameLength)
        .every((b, j) => b === bytes[offset + 46 + j])
    )
      fail('archive');
    extras(local + 30 + nameLength, v.getUint16(local + 28, true));
    const crc = v.getUint32(offset + 16, true);
    let dataEnd = data + compressed;
    if (flags & 8) {
      if (dataEnd + 12 > start) fail('archive');
      if (v.getUint32(dataEnd, true) === 0x08074b50) dataEnd += 4;
      if (
        dataEnd + 12 > start ||
        v.getUint32(dataEnd, true) !== crc ||
        v.getUint32(dataEnd + 4, true) !== compressed ||
        v.getUint32(dataEnd + 8, true) !== size
      )
        fail('archive');
      dataEnd += 12;
    } else if (
      v.getUint32(local + 14, true) !== crc ||
      v.getUint32(local + 18, true) !== compressed ||
      v.getUint32(local + 22, true) !== size
    )
      fail('archive');
    if (method === 0 && size !== compressed) fail('archive');
    if (!name.endsWith('/')) {
      packagePath(name);
      if (seen.has(name.toLowerCase())) fail('duplicate');
      seen.add(name.toLowerCase());
      total += size;
      if (++files > 128 || size > MAX_FILE || total > MAX_SOURCE) fail('size');
    } else {
      if (name.includes('..') || name.startsWith('/') || name.includes('\\')) fail('path');
      if (size !== 0) fail('archive');
    }
    entries.push({ name, size, compressed, data, method, crc, local, end: dataEnd });
    offset = next;
  }
  if (offset !== end || offset - start !== v.getUint32(end + 12, true)) fail('archive');
  // One authoritative directory: no overlapping entries, hidden records or ZIP64 payloads.
  let position = 0;
  for (const entry of [...entries].sort((a, b) => a.local - b.local)) {
    if (entry.local !== position) fail('archive');
    position = entry.end;
  }
  if (position !== start) fail('archive');
  return entries;
}

const crcTable = Uint32Array.from({ length: 256 }, (_, n) => {
  for (let i = 0; i < 8; i++) n = (n >>> 1) ^ (n & 1 ? 0xedb88320 : 0);
  return n >>> 0;
});

export function unpack(bytes: Uint8Array): PackageFiles {
  if (!bytes.length || bytes.length > MAX_ARCHIVE) fail('size');
  try {
    const entries = inspectZip(bytes);
    const files: PackageFiles = Object.create(null);
    let total = 0;
    for (const entry of entries) {
      const output = new Uint8Array(entry.size);
      let written = 0,
        crc = 0xffffffff;
      const consume = (chunk: Uint8Array) => {
        if (written + chunk.length > entry.size || total + chunk.length > MAX_SOURCE) fail('size');
        output.set(chunk, written);
        written += chunk.length;
        total += chunk.length;
        for (const b of chunk) crc = crcTable[(crc ^ b) & 255] ^ (crc >>> 8);
      };
      const content = bytes.subarray(entry.data, entry.data + entry.compressed);
      if (entry.method === 0) consume(content);
      else {
        const decoder = new Inflate(consume);
        // Small compressed chunks bound temporary expansion, including dishonest ZIP sizes.
        for (let i = 0; i < content.length; i += 1024)
          decoder.push(content.subarray(i, i + 1024), i + 1024 >= content.length);
        if (!content.length) fail('archive');
      }
      if (written !== entry.size || (crc ^ 0xffffffff) >>> 0 !== entry.crc) fail('archive');
      if (!entry.name.endsWith('/')) files[entry.name] = output;
    }
    return files;
  } catch (e) {
    if (e instanceof PackageError) throw e;
    return fail('archive');
  }
}

export function validateFiles(input: PackageFiles): Creation {
  const files: PackageFiles = Object.create(null);
  const names = Object.keys(input);
  if (!names.length || names.length > 128) fail('size');
  // A folder selected in the file picker may add one common parent directory.
  const prefix =
    !input['manifest.json'] && names.every((n) => n.startsWith(names[0].split('/')[0] + '/'))
      ? names[0].split('/')[0] + '/'
      : '';
  let total = 0;
  const seen = new Set<string>();
  for (const original of names) {
    const name = packagePath(original.slice(prefix.length));
    if (seen.has(name.toLowerCase())) fail('duplicate');
    seen.add(name.toLowerCase());
    const data = input[original];
    if (
      !(data instanceof Uint8Array) ||
      data.length > MAX_FILE ||
      (total += data.length) > MAX_SOURCE
    )
      fail('size');
    if (/\.png$/i.test(name)) pngSize(data);
    files[name] = data;
  }
  if (!files['manifest.json'] || files['manifest.json'].length > 65536) fail('manifest');
  let m: Record<string, unknown>;
  try {
    m = object(
      JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(files['manifest.json'])),
    );
  } catch {
    return fail('manifest');
  }
  keys(m, [
    '$schema',
    'schemaVersion',
    'kind',
    'id',
    'name',
    'author',
    'version',
    'license',
    'description',
    'theme',
    'mascot',
  ]);
  if (m.schemaVersion !== 1 || !['theme', 'mascot'].includes(String(m.kind).toLowerCase()))
    fail('schema');
  if (
    typeof m.id !== 'string' ||
    !/^[a-z0-9][a-z0-9._-]{2,79}$/.test(m.id) ||
    m.id.startsWith('miniinfos.') ||
    m.id.endsWith('.') ||
    /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\.|$)/.test(m.id)
  )
    fail('id');
  if (
    !text(m.name, 80) ||
    !text(m.author, 80) ||
    !text(m.license, 100) ||
    String(m.license).trim().toUpperCase() === 'UNLICENSED'
  )
    fail('metadata');
  if (
    typeof m.version !== 'string' ||
    !/^\d+\.\d+(\.\d+){0,2}$/.test(m.version) ||
    m.version.length > 40 ||
    m.version.split('.').some((n) => Number(n) > 2147483647)
  )
    fail('version');
  if (
    m.description !== undefined &&
    (typeof m.description !== 'string' || m.description.length > 600)
  )
    fail('metadata');
  const image = (path: unknown) => {
    if (typeof path !== 'string' || !/\.png$/i.test(packagePath(path)) || !files[path])
      return fail('missing-image');
    return pngSize(files[path]);
  };
  m.kind = String(m.kind).toLowerCase();
  if (m.kind === 'theme') {
    if (m.mascot != null) fail('definition');
    const theme = object(m.theme);
    keys(theme, ['base', 'colors', 'headerImage', 'headerHeight', 'headerTitleSize']);
    if (
      !['Simple', 'Light', 'Classic'].includes(String(theme.base ?? 'Simple')) ||
      !number(theme.headerHeight ?? 96, 44, 160) ||
      !number(theme.headerTitleSize ?? 20, 10, 32)
    )
      fail('theme');
    if (
      theme.colors != null &&
      Object.entries(object(theme.colors)).some(
        ([k, c]) =>
          !colorKeys.includes(k) || typeof c !== 'string' || !/^#([a-f\d]{6}|[a-f\d]{8})$/i.test(c),
      )
    )
      fail('colors');
    if (theme.headerImage != null) image(theme.headerImage);
  } else {
    if (m.theme != null) fail('definition');
    const mascot = object(m.mascot);
    keys(mascot, ['spriteSheet', 'columns', 'rows', 'states', 'anatomy']);
    if (!integer(mascot.columns, 1, 16) || !integer(mascot.rows, 1, 10)) fail('grid');
    const [w, h] = image(mascot.spriteSheet);
    if (w % Number(mascot.columns) || h % Number(mascot.rows)) fail('grid');
    const tracks = object(mascot.states);
    if (!tracks.idle) fail('idle');
    for (const [state, value] of Object.entries(tracks)) {
      const track = object(value);
      keys(track, ['row', 'frameDurationMs', 'frames', 'durationsMs', 'blendMs']);
      if (
        !states.includes(state as (typeof states)[number]) ||
        !integer(track.row, 0, Number(mascot.rows) - 1)
      )
        fail('animation');
      const rawFrames = track.frames ?? Array.from({ length: Number(mascot.columns) }, (_, i) => i);
      if (
        !Array.isArray(rawFrames) ||
        !rawFrames.length ||
        rawFrames.length > 32 ||
        rawFrames.some((f) => !integer(f, 0, Number(mascot.columns) - 1))
      )
        fail('animation');
      const frames = rawFrames as number[];
      if (
        !integer(track.frameDurationMs ?? 160, 16, 60000) ||
        !integer(track.blendMs ?? 120, 0, 60000) ||
        (track.durationsMs != null &&
          (!Array.isArray(track.durationsMs) ||
            track.durationsMs.length !== frames.length ||
            track.durationsMs.some((n) => !integer(n, 16, 60000))))
      )
        fail('timing');
    }
    if (mascot.anatomy != null) validateAnatomy(object(mascot.anatomy));
  }
  // Canonical metadata is the exact data previewed and submitted.
  files['manifest.json'] = new TextEncoder().encode(JSON.stringify(m, null, 2) + '\n');
  const archive = zipSync(files, { level: 6 });
  if (archive.length > MAX_ARCHIVE) fail('size');
  return { manifest: m as Manifest, files, archive };
}

function validateAnatomy(a: Record<string, unknown>) {
  keys(a, ['regions', 'anchors', 'massCenter', 'upsideDownMassCenter']);
  const point = (p: unknown) => {
    const v = object(p);
    keys(v, ['x', 'y']);
    if (!number(v.x, 0, 320) || !number(v.y, 0, 320)) fail('anatomy');
  };
  point(a.massCenter);
  point(a.upsideDownMassCenter);
  if (!Array.isArray(a.regions) || a.regions.length > 32) fail('anatomy');
  for (const value of a.regions as unknown[]) {
    const r = object(value);
    keys(r, ['part', 'x', 'y', 'radiusX', 'radiusY']);
    if (
      !['leftEye', 'rightEye', 'feet', 'leftWing', 'rightWing', 'head', 'body'].includes(
        String(r.part),
      ) ||
      !number(r.x, 0, 320) ||
      !number(r.y, 0, 320) ||
      !number(r.radiusX, Number.MIN_VALUE, 640) ||
      !number(r.radiusY, Number.MIN_VALUE, 640)
    )
      fail('anatomy');
  }
  for (const [state, p] of Object.entries(object(a.anchors))) {
    if (!states.includes(state as (typeof states)[number])) fail('anatomy');
    point(p);
  }
}
