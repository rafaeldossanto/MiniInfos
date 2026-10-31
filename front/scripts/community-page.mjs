import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));

/** The landing page remains the source of truth for the shared navigation and footer. */
export function communityPage(template, landing) {
  const extract = (tag) => {
    const match = landing.match(new RegExp('<' + tag + '\\b[\\s\\S]*?</' + tag + '>'));
    if (!match) throw new Error('Missing shared site section: ' + tag);
    return match[0];
  };
  const homeLinks = (html) =>
    html
      .replace(/href="#inicio"/g, 'href="/"')
      .replace(/href="#(recursos|aparencia|duvidas|downloads)"/g, 'href="/#$1"')
      .replace(/href="\/comunidade"/g, 'href="/comunidade" aria-current="page"');
  return template
    .replace('<!-- site:header -->', homeLinks(extract('header') + '\n' + extract('dialog')))
    .replace('<!-- site:footer -->', homeLinks(extract('footer')));
}

export function generateCommunityPage() {
  writeFileSync(
    join(root, 'comunidade.html'),
    communityPage(
      readFileSync(join(root, 'pages/community.html'), 'utf8'),
      readFileSync(join(root, 'index.html'), 'utf8'),
    ),
  );
}
