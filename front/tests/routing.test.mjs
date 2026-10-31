import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { runInNewContext } from 'node:vm';
import { createServer } from 'vite';
import { sitePages, siteRoutes } from '../src/routing.ts';
import { generateEnglishPages } from '../scripts/localized-pages.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const template = readFileSync(join(root, '../infra/site.yml'), 'utf8');
const code = template
  .match(/      FunctionCode: !Sub \|\r?\n([\s\S]*?)\r?\n  Distribution:/)[1]
  .replace(/^        /gm, '')
  .replaceAll('${DomainName}', 'miniinfos.com');
const handler = runInNewContext(code + '\nhandler;');
const request = (uri, querystring = {}, host = 'miniinfos.com') => ({
  method: 'GET',
  uri,
  querystring,
  headers: { host: { value: host } },
});

test('CloudFront serves every clean route from the correct HTML object without a redirect', () => {
  for (const route of siteRoutes) {
    const incoming = request(route.path, { example: { value: '1' } });
    const result = handler({ request: incoming });
    assert.equal(result, incoming);
    assert.equal(result.uri, '/' + route.file);
    assert.equal(result.querystring.example.value, '1');
  }
});

test('legacy HTML and trailing slash links redirect once to their clean URL, preserving query values', () => {
  for (const route of siteRoutes) {
    for (const alias of [
      '/' + route.file,
      route.path.endsWith('/') ? route.path.slice(0, -1) : route.path + '/',
    ].filter(Boolean)) {
      const result = handler({
        request: request(alias, {
          from: { value: 'a%20b' },
          tag: { value: 'one', multiValue: [{ value: 'one' }, { value: 'two' }] },
        }),
      });
      assert.equal(result.statusCode, 301, alias);
      assert.equal(
        result.headers.location.value,
        'https://miniinfos.com' + route.path + '?from=a%20b&tag=one&tag=two',
      );
    }
  }
});

test('www and old page names share a single redirect to the fixed apex; assets and unknown URLs stay unchanged', () => {
  const result = handler({ request: request('/termos.html', {}, 'www.miniinfos.com') });
  assert.equal(result.headers.location.value, 'https://miniinfos.com/termos-de-uso');
  for (const path of [
    '/assets/main-123.js',
    '/favicon.svg',
    '/icons.svg',
    '/not-a-page',
    '/en/not-a-page',
  ]) {
    const incoming = request(path);
    assert.equal(handler({ request: incoming }), incoming);
    assert.equal(incoming.uri, path);
  }
  const externalHost = handler({ request: request('/privacidade.html', {}, 'other.example') });
  assert.equal(
    externalHost.headers.location.value,
    'https://miniinfos.com/politica-de-privacidade',
  );
});

test('public links, canonical URLs and language alternates use clean paths in both languages', () => {
  generateEnglishPages();
  for (const page of sitePages) {
    for (const route of [page.pt, page.en]) {
      const html = readFileSync(join(root, route.file), 'utf8');
      assert.doesNotMatch(html, /href="(?:https:\/\/miniinfos\.com)?\/[^"#]*\.html(?:["#?])/);
      if (route.file.endsWith('404.html')) continue;
      assert.ok(html.includes('rel="canonical" href="https://miniinfos.com' + route.path + '"'));
      for (const language of ['pt', 'en'])
        assert.ok(html.includes('href="https://miniinfos.com' + page[language].path + '"'));
    }
  }
});

test('the development server supports direct clean URLs and redirects legacy links', async () => {
  const server = await createServer({ root, server: { host: '127.0.0.1', port: 0 } });
  try {
    await server.listen();
    const base = 'http://127.0.0.1:' + server.httpServer.address().port;
    const styles = new Set();
    for (const route of siteRoutes) {
      const response = await fetch(base + route.path);
      assert.equal(response.status, 200, route.path);
      const html = await response.text();
      assert.ok(
        html.includes('<html lang="' + (route.path.startsWith('/en/') ? 'en' : 'pt-BR') + '"'),
      );
      // CSS must be obtainable as CSS from the HTML alone, with no execution of the entry module.
      const head = html.match(/<head>[\s\S]*?<\/head>/)[0];
      const sheets = [...head.matchAll(/<link\b[^>]*rel="stylesheet"[^>]*href="([^"]+)"/g)];
      assert.ok(sheets.length >= 3, route.path + ' has no render-blocking layout styles');
      for (const [, href] of sheets) styles.add(href);
      const legacy = await fetch(base + '/' + route.file + '?from=old', { redirect: 'manual' });
      assert.equal(legacy.status, 301);
      assert.equal(legacy.headers.get('location'), route.path + '?from=old');
    }
    for (const href of styles) {
      const response = await fetch(new URL(href, base), { headers: { Accept: 'text/css' } });
      assert.equal(response.status, 200, href);
      assert.match(response.headers.get('content-type'), /text\/css/, href);
      const content = await response.text();
      assert.ok(content.trim().length > 0, href);
      assert.doesNotMatch(
        content,
        /__vite__updateStyle/,
        href + ' returned a JavaScript style injector',
      );
    }
  } finally {
    await server.close();
  }
});
