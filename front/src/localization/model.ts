export type Language = 'pt' | 'en';
export type LanguageChoice = Language | 'auto';
export const LANGUAGE_STORAGE_KEY = 'miniinfos.site.language';
export const LANGUAGE_EVENT = 'miniinfos-language-change';
export function socialImage(language: Language): string {
  return language === 'en' ? '/art/social-preview-en.webp' : '/art/social-preview.webp';
}

export function languageChoice(value: string | null): LanguageChoice {
  return value === 'pt' || value === 'en' ? value : 'auto';
}
export function savedLanguage(storage: Pick<Storage, 'getItem'>): LanguageChoice {
  try {
    return languageChoice(storage.getItem(LANGUAGE_STORAGE_KEY));
  } catch {
    return 'auto';
  }
}
export function resolveLanguage(choice: LanguageChoice, languages: readonly string[]): Language {
  if (choice !== 'auto') return choice;
  for (const locale of languages) {
    const language = locale.toLowerCase().split(/[-_]/)[0];
    if (language === 'pt' || language === 'en') return language;
  }
  return 'en';
}
const pages = [
  { pt: '/', en: '/en/index.html' },
  { pt: '/privacidade.html', en: '/en/privacy.html' },
  { pt: '/termos.html', en: '/en/terms.html' },
  { pt: '/404.html', en: '/en/404.html' },
] as const;
export function localizedPath(path: string, language: Language): string {
  if (path === '/index.html' || path === '/en/') return language === 'en' ? '/en/index.html' : '/';
  return pages.find((page) => page.pt === path || page.en === path)?.[language] ?? path;
}
