import { test } from 'node:test';
import assert from 'node:assert/strict';
import { availablePackages, detectPlatform, packages, releaseApi } from '../src/downloads/model.ts';

test('desktop detection does not mistake phones for installable desktop systems', () => {
  assert.equal(detectPlatform('Win32'), 'windows');
  assert.equal(detectPlatform('MacIntel'), 'macos');
  assert.equal(detectPlatform('Linux x86_64'), 'linux');
  assert.equal(detectPlatform('Linux', 'Android'), null);
  assert.equal(detectPlatform('iPhone'), null);
  assert.equal(detectPlatform('unknown'), null);
});
test('downloads are enabled only for exact native app assets from a published app release', () => {
  const assets = packages.map(({ file }) => ({ name: file, browser_download_url: `https://github.com/rafaeldossanto/MiniInfos/releases/download/v1.16.0/${file}` }));
  assert.equal(availablePackages({ draft: false, prerelease: false, assets }).size, 5);
  assert.equal(availablePackages({ draft: true, assets }).size, 0);
  assert.equal(availablePackages({ prerelease: true, assets }).size, 0);
  assert.equal(availablePackages({ assets: [{ ...assets[0], browser_download_url: 'https://example.com/MiniInfos-Setup.exe' }] }).size, 0);
  assert.equal(availablePackages({ assets: [{ name: 'index.json', browser_download_url: assets[0].browser_download_url }] }).size, 0);
  assert.equal(availablePackages({ assets: [assets[0]] }).has('MiniInfos-macos-arm64.dmg'), false);
  assert.equal(availablePackages(null).size, 0);
  assert.ok(releaseApi.endsWith('/1432597159/releases/latest'));
});
