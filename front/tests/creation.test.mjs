import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { zipSync, Zip, ZipDeflate, ZipPassThrough } from 'fflate';
import {
  unpack,
  validateFiles,
  packagePath,
  pngSize,
  PackageError,
  MAX_ARCHIVE,
} from '../src/community/creation/package.ts';
import { poseAt, cssColor } from '../src/community/creation/preview.ts';
import { serviceBase } from '../src/community/creation/api.ts';

const bytes = (value) => new TextEncoder().encode(JSON.stringify(value));
test('ordinary stored and streaming ZIP packages remain compatible, including data descriptors', () => {
  const content = new TextEncoder().encode('A normal package readme.');
  for (const Entry of [ZipDeflate, ZipPassThrough]) {
    const chunks = [];
    const zip = new Zip((error, chunk) => {
      assert.ifError(error);
      chunks.push(chunk);
    });
    const entry = new Entry('README.md');
    zip.add(entry);
    entry.push(content.subarray(0, 6));
    entry.push(content.subarray(6), true);
    zip.end();
    assert.deepEqual(unpack(new Uint8Array(Buffer.concat(chunks)))['README.md'], content);
  }
  assert.deepEqual(unpack(zipSync({ 'README.md': content }, { level: 0 }))['README.md'], content);
});
test('ZIP extraction rejects ZIP64 locators, inconsistent sizes, CRC damage and excess expansion', () => {
  const original = zipSync({ 'data.txt': new TextEncoder().encode('a small bounded fixture') });
  const central = new DataView(original.buffer).getUint32(original.length - 6, true);
  const mismatched = original.slice();
  new DataView(mismatched.buffer).setUint32(central + 24, 1, true);
  assert.throws(() => unpack(mismatched), PackageError);
  const short = original.slice(),
    sv = new DataView(short.buffer);
  sv.setUint32(22, 1, true);
  sv.setUint32(central + 24, 1, true);
  reject(() => unpack(short), 'size');
  const damaged = original.slice(),
    dv = new DataView(damaged.buffer);
  dv.setUint32(14, 0, true);
  dv.setUint32(central + 16, 0, true);
  reject(() => unpack(damaged), 'archive');
  // The locator must be rejected even when embedded in a directory comment.
  const end = original.length - 22,
    locator = new Uint8Array(original.length + 20);
  locator.set(original.subarray(0, end));
  locator.set(original.subarray(end), end + 20);
  const lv = new DataView(locator.buffer);
  lv.setUint16(central + 32, 20, true);
  lv.setUint32(end, 0x07064b50, true);
  lv.setUint32(end + 20 + 12, end - central + 20, true);
  reject(() => unpack(locator), 'archive');
});
const theme = () =>
  JSON.parse(
    readFileSync(
      new URL('../../marketplace/packages/community.ocean/manifest.json', import.meta.url),
    ),
  );
const files = (m) => ({ 'manifest.json': bytes(m) });
const reject = (fn, code) => assert.throws(fn, (e) => e instanceof PackageError && e.code === code);
const buster = () => ({
  'manifest.json': new Uint8Array(
    readFileSync(
      new URL('../../marketplace/packages/community.buster-beaver/manifest.json', import.meta.url),
    ),
  ),
  'sprites.png': new Uint8Array(
    readFileSync(
      new URL('../../marketplace/packages/community.buster-beaver/sprites.png', import.meta.url),
    ),
  ),
});

test('creator roundtrips real theme and pet packages, metadata and images without remote resources', () => {
  for (const source of [files(theme()), buster()]) {
    const creation = validateFiles(source);
    assert.ok(creation.archive.length <= MAX_ARCHIVE);
    const restored = validateFiles(unpack(creation.archive));
    assert.deepEqual(restored.manifest, creation.manifest);
    assert.deepEqual(restored.files, creation.files);
  }
  const folder = validateFiles({ 'my-theme/manifest.json': bytes(theme()) });
  assert.ok(folder.files['manifest.json']);
});

test('Buster has sixteen genuine head-turn cells, a compatible atlas and explicit unchanged tracks', () => {
  const source = buster();
  const creation = validateFiles(source);
  const mascot = creation.manifest.mascot;
  assert.equal(mascot.columns, 16);
  assert.equal(creation.manifest.version, '1.0.1');
  assert.deepEqual(pngSize(source['sprites.png']), [5120, 3200]);
  for (const [name, track] of Object.entries(mascot.states)) {
    const turning = name === 'idle' || name === 'alert';
    assert.equal(track.frames.length, turning ? 16 : 8);
    assert.equal(track.durationsMs.length, track.frames.length);
    assert.deepEqual(
      track.frames,
      Array.from({ length: track.frames.length }, (_, i) => i),
    );
    if (turning) assert.equal(track.blendMs, 0);
  }
});
test('creator blocks traversal, alternate separators, duplicate case names, scripts and symbolic links', () => {
  for (const name of [
    '../manifest.json',
    '/tmp.json',
    'a\\b.json',
    'a/./b.json',
    'a/nul.png',
    'a:stream.txt',
    '.github/x.json',
    'a\n.png',
    'image.svg',
    'evil.exe',
  ])
    assert.throws(() => packagePath(name));
  reject(() => validateFiles({ ...files(theme()), 'MANIFEST.json': bytes(theme()) }), 'duplicate');
  for (const source of [
    { '../manifest.json': bytes(theme()) },
    { 'evil.exe': new Uint8Array(1) },
    { 'manifest.json': bytes(theme()), 'MANIFEST.json': bytes(theme()) },
  ])
    assert.throws(() => unpack(zipSync(source)));
  const link = zipSync(files(theme()));
  const v = new DataView(link.buffer, link.byteOffset, link.byteLength);
  for (let i = 0; i < link.length - 46; i++)
    if (v.getUint32(i, true) === 0x02014b50) {
      v.setUint32(i + 38, 0xa1ff0000, true);
      break;
    }
  reject(() => unpack(link), 'archive');
});
test('malformed archives and inflated metadata are bounded before extraction', () => {
  for (const content of [new Uint8Array(1), new Uint8Array(MAX_ARCHIVE + 1)])
    assert.throws(() => unpack(content));
  const bomb = zipSync(files(theme()));
  const v = new DataView(bomb.buffer, bomb.byteOffset, bomb.byteLength);
  for (let i = 0; i < bomb.length - 46; i++)
    if (v.getUint32(i, true) === 0x02014b50) {
      v.setUint32(i + 24, 0x7fffffff, true);
      break;
    }
  assert.throws(() => unpack(bomb), PackageError);
  reject(
    () =>
      validateFiles(
        Object.fromEntries(Array.from({ length: 129 }, (_, i) => [i + '.txt', new Uint8Array()])),
      ),
    'size',
  );
});
test('metadata, licenses, definitions and theme colors receive actionable validation', () => {
  const cases = [
    ['schemaVersion', 2, 'schema'],
    ['id', 'miniinfos.forged', 'id'],
    ['id', 'con.foo', 'id'],
    ['license', 'UNLICENSED', 'metadata'],
    ['version', '1.0.99999999999', 'version'],
    ['unknown', true, 'fields'],
    ['name', '\n', 'metadata'],
  ];
  for (const [key, value, code] of cases) {
    const m = theme();
    m[key] = value;
    reject(() => validateFiles(files(m)), code);
  }
  const m = theme();
  m.theme.colors.Accent = 'url(https://example.com)';
  reject(() => validateFiles(files(m)), 'colors');
  const image = theme();
  image.theme.headerImage = 'missing.png';
  reject(() => validateFiles(files(image)), 'missing-image');
});
test('pet validation checks grid, animation rows, timing, PNG dimensions and anatomy', () => {
  const modify = (fn) => {
    const source = buster();
    const m = JSON.parse(new TextDecoder().decode(source['manifest.json']));
    fn(m);
    source['manifest.json'] = bytes(m);
    return source;
  };
  reject(() => validateFiles(modify((m) => (m.mascot.columns = 7))), 'grid');
  reject(() => validateFiles(modify((m) => delete m.mascot.states.idle)), 'idle');
  reject(() => validateFiles(modify((m) => (m.mascot.states.idle.frames = [99]))), 'animation');
  reject(() => validateFiles(modify((m) => (m.mascot.states.idle.durationsMs = [1]))), 'timing');
  reject(() => validateFiles(modify((m) => (m.mascot.anatomy.massCenter.x = Infinity))), 'anatomy');
  reject(() => pngSize(new Uint8Array(35)), 'png');
});
test('preview honors nonuniform timing, frame order, wrap and WPF alpha color order', () => {
  const track = { row: 0, frames: [2, 1, 0], durationsMs: [100, 200, 300], blendMs: 100 };
  assert.deepEqual(poseAt(track, 3, 0), { frame: 2, next: 1, mix: 0 });
  assert.equal(poseAt(track, 3, 100).frame, 1);
  assert.ok(Math.abs(poseAt(track, 3, 250).mix - 0.5) < 1e-12);
  assert.equal(poseAt(track, 3, 300).frame, 0);
  assert.equal(poseAt(track, 3, 600).frame, 2);
  assert.equal(cssColor('#80224466'), '#22446680');
  assert.equal(cssColor('#224466'), '#224466');
});
test('submission endpoint configuration excludes credentials, insecure URLs and query strings', () => {
  assert.equal(serviceBase('https://community.example.com/'), 'https://community.example.com');
  for (const value of [
    'http://example.com',
    'https://secret@example.com',
    'https://example.com/?token=secret',
    'javascript:alert(1)',
    undefined,
  ])
    assert.equal(serviceBase(value), '');
});
