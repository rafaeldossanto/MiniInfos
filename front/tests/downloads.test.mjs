import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  availablePackages,
  detectPlatform,
  downloadTarget,
  packages,
  releaseApi,
} from '../src/downloads/model.ts';

test('desktop detection does not mistake phones for installable desktop systems', () => {
  assert.equal(detectPlatform('Win32'), 'windows');
  assert.equal(detectPlatform('MacIntel'), 'macos');
  assert.equal(detectPlatform('Linux x86_64'), 'linux');
  assert.equal(detectPlatform('Linux', 'Android'), null);
  assert.equal(detectPlatform('iPhone'), null);
  assert.equal(detectPlatform('unknown'), null);
});

test('modern browser hints choose the correct desktop download and retain the architecture choice', () => {
  for (const [hint, expected] of [
    ['Windows', 'windows'],
    ['macOS', 'macos'],
    ['Linux', 'linux'],
  ]) {
    const platform = detectPlatform('', '', { platform: hint, mobile: false });
    assert.equal(platform, expected);
    assert.equal(downloadTarget(platform), '#download-' + expected);
  }
  assert.equal(detectPlatform('Win32', '', { platform: 'macOS' }), 'macos');
  assert.equal(downloadTarget(null), '#downloads');
});

test('desktop-mode iPads, mobile hints, Chrome OS and X11 alone do not recommend a desktop installer', () => {
  assert.equal(
    detectPlatform('MacIntel', 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15)', {
      maxTouchPoints: 5,
    }),
    null,
  );
  assert.equal(detectPlatform('MacIntel', '', { maxTouchPoints: 0 }), 'macos');
  assert.equal(detectPlatform('Win32', '', { mobile: true }), null);
  assert.equal(detectPlatform('Linux x86_64', '', { platform: 'Android' }), null);
  assert.equal(
    detectPlatform('Linux x86_64', 'Mozilla/5.0 (X11; CrOS x86_64)', { platform: 'Chrome OS' }),
    null,
  );
  assert.equal(detectPlatform('X11', 'FreeBSD'), null);
  assert.equal(detectPlatform('WinCE', 'Windows Phone 10.0'), null);
});
test('downloads are enabled only for exact native app assets from a published app release', () => {
  const assets = packages.map(({ file }) => ({
    name: file,
    browser_download_url: `https://github.com/rafaeldossanto/MiniInfos/releases/download/v1.16.0/${file}`,
  }));
  assert.equal(availablePackages({ draft: false, prerelease: false, assets }).size, 5);
  assert.equal(availablePackages({ draft: true, assets }).size, 0);
  assert.equal(availablePackages({ prerelease: true, assets }).size, 0);
  assert.equal(
    availablePackages({
      assets: [{ ...assets[0], browser_download_url: 'https://example.com/MiniInfos-Setup.exe' }],
    }).size,
    0,
  );
  assert.equal(
    availablePackages({
      assets: [{ name: 'index.json', browser_download_url: assets[0].browser_download_url }],
    }).size,
    0,
  );
  assert.equal(availablePackages({ assets: [assets[0]] }).has('MiniInfos-macos-arm64.dmg'), false);
  assert.equal(availablePackages(null).size, 0);
  assert.ok(releaseApi.endsWith('/1432597159/releases/latest'));
});
