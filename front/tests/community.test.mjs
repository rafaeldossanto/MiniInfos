import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import {
  readCatalog,
  publishedCatalog,
  releaseAssets,
  galleryItems,
  selectItems,
  assetUrl,
} from '../src/community/model.ts';
import { fetchCatalog } from '../scripts/catalog.mjs';
import { communityPage } from '../scripts/community-page.mjs';
import { englishHtml } from '../scripts/localized-pages.mjs';
import { localizedPath } from '../src/localization/model.ts';

const root = fileURLToPath(new URL('../', import.meta.url));
const packageEntry = (overrides = {}) => ({
  id: 'community.ocean',
  kind: 'theme',
  name: 'Oceano',
  author: 'MiniInfos Community',
  version: '1.0.0',
  license: 'CC0-1.0',
  description: 'Azul e verde.',
  asset: 'community.ocean-1.0.0.miniinfos',
  size: 494,
  sha256: 'a'.repeat(64),
  preview: 'community.ocean-1.0.0.png',
  previewSha256: 'b'.repeat(64),
  ...overrides,
});
const index = (packages = [packageEntry()]) => ({
  schemaVersion: 1,
  generatedAt: '2026-10-26T22:50:50Z',
  packages,
});
const release = () => ({
  tag_name: 'catalog',
  draft: false,
  prerelease: true,
  assets: [
    {
      name: 'index.json',
      size: Buffer.byteLength(JSON.stringify(index())),
      browser_download_url: assetUrl('index.json'),
    },
    {
      name: packageEntry().asset,
      size: 494,
      created_at: '2026-10-27T12:00:00Z',
      browser_download_url: assetUrl(packageEntry().asset),
    },
    {
      name: packageEntry().preview,
      size: 1200,
      browser_download_url: assetUrl(packageEntry().preview),
    },
  ],
});

test('the site snapshot follows the published catalog contract, including authorship and license', () => {
  const snapshot = JSON.parse(readFileSync(join(root, 'public/community/catalog.json'), 'utf8'));
  const catalog = readCatalog(snapshot);
  for (const item of catalog.packages) {
    assert.ok(item.author && item.license);
    assert.ok(assetUrl(item.asset).includes('/releases/download/catalog/'));
    assert.ok(!assetUrl(item.preview).includes('/latest/'));
  }
});

test('invalid schemas, identities, types, versions, sizes and paths do not become downloadable packages', () => {
  assert.throws(() => readCatalog({ ...index(), schemaVersion: 2 }));
  assert.throws(() => readCatalog(index([packageEntry(), packageEntry()])));
  for (const override of [
    { kind: 'script' },
    { id: '../file' },
    { id: 'miniinfos.official' },
    { asset: 'https://other.example/evil.miniinfos' },
    { preview: 'malware.svg' },
    { version: 'latest' },
    { sha256: 'bad' },
    { size: 0 },
    { size: 8 * 1024 * 1024 + 1 },
    { size: 2.5 },
    { license: 'UNLICENSED' },
    { name: '' },
  ])
    assert.throws(() => readCatalog(index([packageEntry(override)])), JSON.stringify(override));
  assert.throws(() => assetUrl('../theme.miniinfos'));
});

test('only a published fixed catalog release with matching actual assets can supply download links', () => {
  for (const override of [{ tag_name: 'v2.0.0' }, { draft: true }, { prerelease: false }])
    assert.throws(() => releaseAssets({ ...release(), ...override }));
  const external = release();
  external.assets[1].browser_download_url = 'https://other.example/package.miniinfos';
  assert.throws(() => releaseAssets(external));
  const assets = releaseAssets(release());
  assert.equal(publishedCatalog(index(), assets).packages[0].publishedAt, '2026-10-27T12:00:00Z');
  assert.throws(() => publishedCatalog(index(), assets.slice(0, 2)));
  assert.throws(() => publishedCatalog(index(), [...assets, assets[1]]));
  assert.throws(() => publishedCatalog(index([packageEntry({ size: 495 })]), assets));
});

test('catalog synchronization requests only release metadata and index, without downloading every package or preview', async () => {
  const requested = [];
  const catalog = await fetchCatalog(async (address) => {
    requested.push(address);
    return Response.json(address.endsWith('/releases/tags/catalog') ? release() : index());
  });
  assert.equal(catalog.packages.length, 1);
  assert.equal(requested.length, 2);
  assert.ok(requested[0].endsWith('/repositories/1432597159/releases/tags/catalog'));
  assert.ok(requested[1].endsWith('/catalog/index.json'));
  await assert.rejects(
    fetchCatalog(async () => Response.json({ ...release(), tag_name: 'latest' })),
  );
});

test('search matches accents, authors and multiple terms; kind and sorting remain independent', () => {
  const items = galleryItems(readCatalog(index()));
  assert.equal(selectItems(items, 'all', 'classico', 'featured', 'pt').length, 2);
  assert.equal(selectItems(items, 'theme', 'community oceano', 'name', 'pt')[0].name, 'Oceano');
  assert.equal(selectItems(items, 'mascot', 'oceano', 'featured', 'pt').length, 0);
  assert.equal(selectItems(items, 'all', '', 'recent', 'pt')[0].id, 'community.ocean');
  assert.deepEqual(selectItems(items, 'all', 'not-present', 'featured', 'en'), []);
  assert.equal(selectItems(items, 'all', '', 'featured', 'pt')[0].id, 'included.classic-dark');
  assert.ok(galleryItems(null).every((item) => !item.published));
});

test('community shares the existing navigation and footer and preserves home anchors in both languages', () => {
  const html = communityPage(
    readFileSync(join(root, 'pages/community.html'), 'utf8'),
    readFileSync(join(root, 'index.html'), 'utf8'),
  );
  assert.ok(html.includes('href="/comunidade" aria-current="page"'));
  assert.ok(html.includes('href="/#aparencia"'));
  assert.ok(html.includes('https://x.com/rafael_devno'));
  const english = englishHtml(html);
  assert.ok(english.includes('href="/en/community" aria-current="page"'));
  assert.ok(english.includes('href="/en/#aparencia"'));
  assert.ok(english.includes('placeholder="Search themes and pets"'));
  assert.ok(
    english.includes('<title data-i18n="Comunidade — Temas e mascotes para o MiniInfos">Community'),
  );
  assert.equal(localizedPath('/comunidade?q=coruja#temas', 'en'), '/en/community?q=coruja#temas');
  assert.equal(localizedPath('/en/#aparencia', 'pt'), '/#aparencia');
});
