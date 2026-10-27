import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { savedLanguage, resolveLanguage, localizedPath } from '../src/localization/model.ts';
import { setActiveLanguage, compactNumber, t } from '../src/localization/messages.ts';
import { englishHtml } from '../scripts/localized-pages.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
test('translation templates preserve all named values', () => {
  const catalog = JSON.parse(readFileSync(join(root, 'src/localization/en.json'), 'utf8'));
  const placeholders = (text) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();
  for (const [source, english] of Object.entries(catalog))
    assert.deepEqual(placeholders(source), placeholders(english), source);
});
test('automatic language follows browser preference order with an English fallback', () => {
  assert.equal(resolveLanguage('auto', ['pt-PT', 'en-US']), 'pt');
  assert.equal(resolveLanguage('auto', ['en-GB', 'pt-BR']), 'en');
  assert.equal(resolveLanguage('auto', ['fr-FR', 'pt-BR', 'en']), 'pt');
  assert.equal(resolveLanguage('auto', ['ja-JP']), 'en');
  assert.equal(resolveLanguage('auto', []), 'en');
});
test('a saved explicit language overrides detection and invalid or blocked storage restores automatic', () => {
  const saved = savedLanguage({ getItem: () => 'en' });
  assert.equal(resolveLanguage(saved, ['pt-BR']), 'en');
  assert.equal(resolveLanguage('pt', ['en']), 'pt');
  assert.equal(savedLanguage({ getItem: () => 'invalid' }), 'auto');
  assert.equal(
    savedLanguage({
      getItem: () => {
        throw new Error('blocked');
      },
    }),
    'auto',
  );
});
test('locale links stay in the selected language and do not change assets or unknown paths', () => {
  assert.equal(localizedPath('/', 'en'), '/en/index.html');
  assert.equal(localizedPath('/en/privacy.html', 'pt'), '/privacidade.html');
  assert.equal(localizedPath('/termos.html', 'en'), '/en/terms.html');
  assert.equal(localizedPath('/art/athena-dark.webp', 'en'), '/art/athena-dark.webp');
  assert.equal(localizedPath('/missing', 'pt'), '/missing');
});
test('English and Portuguese numbers and demo templates use the same chosen language', () => {
  setActiveLanguage('en');
  assert.equal(compactNumber(69_100_000), '69.1M');
  assert.equal(t('reinicia em {time}', { time: '3h 8min' }), 'resets in 3h 8min');
  setActiveLanguage('pt');
  assert.equal(compactNumber(69_100_000), '69,1M');
  assert.equal(t('Janela 5h'), 'Janela 5h');
});
for (const file of ['index.html', 'privacidade.html', 'termos.html', '404.html']) {
  test(file + ' has complete translations and produces a readable static English page', () => {
    const translated = englishHtml(readFileSync(join(root, file), 'utf8'));
    assert.ok(translated.includes('<html lang="en"'));
    assert.ok(!/<title[^>]*>(?:Privacidade|Termos de uso|Página não encontrada)/.test(translated));
    assert.ok(translated.includes('value="pt"') && translated.includes('value="en"'));
    if (file === 'index.html') {
      assert.ok(/>Your AI usage,</.test(translated));
      assert.ok(translated.includes('href="/en/privacy.html"'));
      assert.ok(translated.includes('https://miniinfos.com/art/social-preview-en.webp'));
      assert.ok(existsSync(join(root, 'public/art/social-preview-en.webp')));
      assert.ok(translated.includes('hreflang="pt-BR" href="https://miniinfos.com/"'));
      assert.ok(translated.includes('rel="canonical" href="https://miniinfos.com/en/index.html"'));
    }
  });
}
