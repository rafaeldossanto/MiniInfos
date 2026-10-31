import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { localizedPath, socialImage } from '../src/localization/model.ts';
import { sitePages } from '../src/routing.ts';
import { generateCommunityPage } from './community-page.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const english = JSON.parse(readFileSync(join(root, 'src/localization/en.json'), 'utf8'));
const decode = (text) =>
  text
    .replace(/&quot;/g, '"')
    .replace(/&#x27;|&#39;|&apos;/g, "'")
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&');
const encode = (text) =>
  text.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const message = (key) => {
  if (!Object.hasOwn(english, key)) throw new Error('Missing English translation: ' + key);
  return english[key];
};

/** Build English pages from the Portuguese layout and the same catalog used in the browser. */
export function englishHtml(source) {
  let html = source.replace(/<html lang="pt-BR"/, '<html lang="en"');
  html = html.replace(
    /<([a-z][\w-]*)([^>]*\bdata-i18n="([^"]+)"[^>]*)>[^<]*<\/\1>/g,
    (_, tag, attributes, key) => `<${tag}${attributes}>${encode(message(decode(key)))}</${tag}>`,
  );
  html = html.replace(/<[^>]+>/g, (tag) => {
    for (const attribute of ['aria-label', 'alt', 'title', 'content', 'placeholder']) {
      const marker = tag.match(new RegExp('data-i18n-' + attribute + '="([^"]*)"'));
      if (!marker) continue;
      const key = decode(marker[1]);
      tag = tag.replace(
        new RegExp('(?<![\\w-])' + attribute + '="[^"]*"'),
        attribute + '="' + encode(message(key)) + '"',
      );
    }
    return tag;
  });
  html = html.replace(/\bhref="(\/[^"]*)"/g, (_, path) => `href="${localizedPath(path, 'en')}"`);
  html = html.replace(/<(link|meta)\b[^>]*>/g, (tag) => {
    if (/property="og:image"/.test(tag))
      return tag.replace(
        /content="[^"]*"/,
        'content="https://miniinfos.com' + socialImage('en') + '"',
      );
    if (!/(?:rel="canonical"|property="og:url")/.test(tag)) return tag;
    return tag.replace(
      /https:\/\/miniinfos\.com(\/[^"\s<>]*)/g,
      (_, path) => 'https://miniinfos.com' + localizedPath(path, 'en'),
    );
  });
  return html;
}

export function generateEnglishPages() {
  generateCommunityPage();
  mkdirSync(join(root, 'en'), { recursive: true });
  for (const page of sitePages)
    writeFileSync(
      join(root, page.en.file),
      englishHtml(readFileSync(join(root, page.pt.file), 'utf8')),
    );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) generateEnglishPages();
