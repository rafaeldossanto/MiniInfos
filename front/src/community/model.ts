import { project } from '../config.ts';

export type PackageKind = 'theme' | 'mascot';
export type CatalogFilter = PackageKind | 'all';
export type CatalogSort = 'featured' | 'recent' | 'name';
export const catalogApi = `https://api.github.com/repositories/${project.repositoryId}/releases/tags/catalog`;
export const catalogRelease = project.repository + '/releases/tag/catalog';
const assetBase = project.repository + '/releases/download/catalog/';
export const MAX_INDEX_BYTES = 1024 * 1024;

export interface PublishedPackage {
  id: string;
  kind: PackageKind;
  name: string;
  author: string;
  version: string;
  license: string;
  description: string;
  asset: string;
  size: number;
  sha256: string;
  preview: string;
  previewSha256: string;
  publishedAt: string;
}
export interface Catalog {
  schemaVersion: 1;
  generatedAt: string;
  packages: PublishedPackage[];
}
type ReleaseAsset = {
  name: string;
  browser_download_url: string;
  size: number;
  created_at?: string;
};

function record(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value))
    throw new Error('Invalid catalog object');
  return value as Record<string, unknown>;
}
function text(value: unknown, max: number, empty = false): string {
  if (typeof value !== 'string' || (!empty && !value.trim()) || value.length > max)
    throw new Error('Invalid catalog text');
  return value;
}
function date(value: unknown): string {
  const result = text(value, 64);
  if (!Number.isFinite(Date.parse(result))) throw new Error('Invalid catalog date');
  return result;
}
function hash(value: unknown): string {
  const result = text(value, 64);
  if (!/^[a-f\d]{64}$/i.test(result)) throw new Error('Invalid catalog hash');
  return result;
}

export function assetUrl(file: string): string {
  if (!/^[a-z0-9][a-z0-9._-]*\.(?:miniinfos|png|json)$/.test(file))
    throw new Error('Invalid catalog asset');
  return assetBase + file;
}

/** Reject paths, executable links, duplicate identities and future schemas before rendering anything. */
export function readCatalog(value: unknown): Catalog {
  const data = record(value);
  if (data.schemaVersion !== 1 || !Array.isArray(data.packages) || data.packages.length > 2000)
    throw new Error('Unsupported catalog');
  const generatedAt = date(data.generatedAt);
  const ids = new Set<string>();
  const packages = data.packages.map((value): PublishedPackage => {
    const item = record(value);
    const id = text(item.id, 80);
    const version = text(item.version, 40);
    if (
      !/^[a-z0-9][a-z0-9._-]{2,79}$/.test(id) ||
      id.startsWith('miniinfos.') ||
      id.endsWith('.') ||
      ids.has(id)
    )
      throw new Error('Invalid package ID');
    ids.add(id);
    if (
      !/^\d+\.\d+(?:\.\d+){0,2}$/.test(version) ||
      (item.kind !== 'theme' && item.kind !== 'mascot')
    )
      throw new Error('Invalid package version or kind');
    const license = text(item.license, 100);
    if (license.trim().toUpperCase() === 'UNLICENSED') throw new Error('Missing package license');
    if (item.asset !== `${id}-${version}.miniinfos` || item.preview !== `${id}-${version}.png`)
      throw new Error('Invalid package files');
    if (
      typeof item.size !== 'number' ||
      !Number.isSafeInteger(item.size) ||
      item.size <= 0 ||
      item.size > 8 * 1024 * 1024
    )
      throw new Error('Invalid package size');
    return {
      id,
      version,
      kind: item.kind,
      license,
      name: text(item.name, 80),
      author: text(item.author, 80),
      description: text(item.description, 600, true),
      asset: item.asset,
      preview: item.preview,
      size: item.size,
      sha256: hash(item.sha256),
      previewSha256: hash(item.previewSha256),
      publishedAt: date(item.publishedAt ?? generatedAt),
    };
  });
  return { schemaVersion: 1, generatedAt, packages };
}

/** Only the fixed prerelease catalog can supply packages; app/latest is never a fallback. */
export function releaseAssets(value: unknown): ReleaseAsset[] {
  const release = record(value);
  if (
    release.tag_name !== 'catalog' ||
    release.draft !== false ||
    release.prerelease !== true ||
    !Array.isArray(release.assets)
  )
    throw new Error('Catalog release not published');
  return release.assets.map((value) => {
    const item = record(value);
    const name = text(item.name, 180);
    if (
      item.browser_download_url !== assetUrl(name) ||
      typeof item.size !== 'number' ||
      item.size <= 0
    )
      throw new Error('Invalid release asset');
    return {
      name,
      size: item.size,
      browser_download_url: item.browser_download_url,
      created_at: typeof item.created_at === 'string' ? item.created_at : undefined,
    };
  });
}

export function publishedCatalog(index: unknown, assets: ReleaseAsset[]): Catalog {
  const catalog = readCatalog(index);
  const unique = (name: string) => {
    const matches = assets.filter((asset) => asset.name === name);
    if (matches.length !== 1) throw new Error('Missing or duplicate release asset: ' + name);
    return matches[0];
  };
  return {
    ...catalog,
    packages: catalog.packages.map((item) => {
      const archive = unique(item.asset);
      const preview = unique(item.preview);
      if (archive.size !== item.size || preview.size > MAX_INDEX_BYTES)
        throw new Error('Invalid published asset size');
      return { ...item, publishedAt: date(archive.created_at ?? catalog.generatedAt) };
    }),
  };
}

export interface GalleryItem {
  id: string;
  kind: PackageKind;
  name: string;
  author: string;
  description: string;
  appearance?: 'classic-dark' | 'classic-light' | 'simple' | 'light';
  owl?: boolean;
  published?: PublishedPackage;
  rank: number;
}

export const includedItems: readonly GalleryItem[] = [
  {
    id: 'included.classic-dark',
    kind: 'theme',
    name: 'Clássico escuro',
    author: 'MiniInfos',
    appearance: 'classic-dark',
    rank: 0,
    description: 'Atena em gravura, cores escuras e detalhes em marfim.',
  },
  {
    id: 'included.classic-light',
    kind: 'theme',
    name: 'Clássico claro',
    author: 'MiniInfos',
    appearance: 'classic-light',
    rank: 1,
    description: 'Atena em mármore e uma paleta clara para o cartão.',
  },
  {
    id: 'included.simple',
    kind: 'theme',
    name: 'Simples',
    author: 'MiniInfos',
    appearance: 'simple',
    rank: 2,
    description: 'Um cartão escuro e direto, com barras em azul.',
  },
  {
    id: 'included.light',
    kind: 'theme',
    name: 'Claro',
    author: 'MiniInfos',
    appearance: 'light',
    rank: 3,
    description: 'Um cartão claro, sem ilustração no cabeçalho.',
  },
  {
    id: 'included.athena-owl',
    kind: 'mascot',
    name: 'Coruja de Atena',
    author: 'MiniInfos',
    owl: true,
    rank: 0,
    description: 'Uma pequena companhia que pisca, reage ao toque e acompanha seu trabalho.',
  },
];

export function galleryItems(catalog: Catalog | null): GalleryItem[] {
  return [
    ...includedItems,
    ...(catalog?.packages ?? []).map((item): GalleryItem => ({
      id: item.id,
      kind: item.kind,
      name: item.name,
      author: item.author,
      description: item.description,
      published: item,
      rank: 10,
    })),
  ];
}
const normalize = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLocaleLowerCase()
    .trim();

export function selectItems(
  items: readonly GalleryItem[],
  filter: CatalogFilter,
  query: string,
  sort: CatalogSort,
  language: string,
): GalleryItem[] {
  const terms = normalize(query).split(/\s+/).filter(Boolean);
  const collator = new Intl.Collator(language, { numeric: true, sensitivity: 'base' });
  return items
    .filter(
      (item) =>
        (filter === 'all' || item.kind === filter) &&
        terms.every((term) =>
          normalize(`${item.name} ${item.author} ${item.description}`).includes(term),
        ),
    )
    .sort((a, b) => {
      if (sort === 'featured' && a.rank !== b.rank) return a.rank - b.rank;
      if (sort === 'recent') {
        const difference =
          Date.parse(b.published?.publishedAt ?? '1970-01-01') -
          Date.parse(a.published?.publishedAt ?? '1970-01-01');
        if (difference) return difference;
      }
      return collator.compare(a.name, b.name);
    });
}
