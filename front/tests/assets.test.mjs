import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { siteRoute } from '../src/routing.ts';
import { generateEnglishPages } from '../scripts/localized-pages.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
// Pages built from a template (Comunidade and the English copies) exist only once generated, as in the build.
generateEnglishPages();
const sources = [
  'index.html',
  'privacidade.html',
  'termos.html',
  '404.html',
  'pages/community.html',
  'src/styles/base.css',
  'src/styles/landing.css',
  'src/styles/widget.css',
];

test('every local page, font and illustration reference exists in the production source', () => {
  for (const name of sources) {
    const content = readFileSync(join(root, name), 'utf8');
    const references = [
      ...content.matchAll(/(?:src|href)=["'](\/[^"']+)["']|url\(['"]?(\/[^)'"]+)['"]?\)/g),
    ];
    for (const reference of references) {
      const relative = (reference[1] ?? reference[2]).split('#')[0].slice(1);
      const page = siteRoute('/' + relative);
      const target = page
        ? join(root, page.file)
        : relative.startsWith('src/')
          ? join(root, relative)
          : join(root, 'public', relative);
      assert.ok(existsSync(target), name + ' references a missing asset: ' + relative);
    }
  }
});

test('all internal navigation anchors point to a real section', () => {
  const html = readFileSync(join(root, 'index.html'), 'utf8');
  const ids = new Set([...html.matchAll(/\bid="([^"]+)"/g)].map((match) => match[1]));
  for (const match of html.matchAll(/href="#([^"]+)"/g))
    assert.ok(ids.has(match[1]), 'Missing section: ' + match[1]);
});

test('every page loads layout styles in the head before deferred JavaScript can run', () => {
  const pages = [
    'index.html',
    'privacidade.html',
    'termos.html',
    '404.html',
    'pages/community.html',
  ];
  for (const page of pages) {
    const html = readFileSync(join(root, page), 'utf8');
    const head = html.match(/<head>[\s\S]*?<\/head>/)[0];
    const sheets = [...head.matchAll(/<link\b[^>]*rel="stylesheet"[^>]*>/g)].map((match) => {
      assert.doesNotMatch(match[0], /\b(?:disabled|media|onload)\s*=/, page);
      return match[0].match(/href="([^"]+)"/)[1];
    });
    for (const shared of ['base', 'landing', 'language'])
      assert.ok(
        sheets.includes('/src/styles/' + shared + '.css'),
        page + ' needs blocking ' + shared + ' styles',
      );
    if (page === 'index.html')
      for (const style of ['widget', 'card-carousel', 'conversations', 'downloads'])
        assert.ok(sheets.includes('/src/styles/' + style + '.css'), page + ': ' + style);
    if (page === 'pages/community.html')
      for (const style of ['widget', 'community', 'creation'])
        assert.ok(sheets.includes('/src/styles/' + style + '.css'), page + ': ' + style);
    assert.ok(html.indexOf('</head>') < html.indexOf('<body'), page);
  }
});
