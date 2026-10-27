import { test } from 'node:test';
import assert from 'node:assert/strict';
import { savedTheme, revealGeometry } from '../src/theme/model.ts';
import { project } from '../src/config.ts';

test('a first visit starts in dark, even when no preference can be read', () => {
  assert.equal(savedTheme({ getItem: () => null }), 'dark');
  assert.equal(savedTheme({ getItem: () => 'unexpected' }), 'dark');
  assert.equal(
    savedTheme({
      getItem: () => {
        throw new DOMException('Storage blocked');
      },
    }),
    'dark',
  );
});

test('the explicit light choice survives the next visit', () => {
  assert.equal(savedTheme({ getItem: () => 'light' }), 'light');
  assert.equal(savedTheme({ getItem: () => 'dark' }), 'dark');
});

for (const viewport of [
  { width: 1440, height: 900 },
  { width: 375, height: 812 },
]) {
  test(
    'a theme reveal covers all corners at ' +
      viewport.width +
      'px, including keyboard and outside click origins',
    () => {
      for (const origin of [
        { x: 0, y: 0 },
        { x: viewport.width, y: 20 },
        { x: viewport.width / 2, y: viewport.height / 2 },
        { x: -10, y: 4000 },
      ]) {
        const { x, y, radius } = revealGeometry(origin, viewport);
        assert.ok(x >= 0 && x <= viewport.width && y >= 0 && y <= viewport.height);
        for (const corner of [
          { x: 0, y: 0 },
          { x: viewport.width, y: 0 },
          { x: 0, y: viewport.height },
          { x: viewport.width, y: viewport.height },
        ]) {
          assert.ok(Math.hypot(corner.x - x, corner.y - y) < radius);
        }
      }
    },
  );
}

test('download goes to the latest app installer, never the catalog', () => {
  const target = new URL(project.download);
  assert.equal(target.protocol, 'https:');
  assert.equal(target.hostname, 'github.com');
  assert.ok(target.pathname.endsWith('/releases/latest/download/MiniInfos-Setup.exe'));
  assert.ok(!target.pathname.includes('/catalog/'));
});
