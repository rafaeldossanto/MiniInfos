import { sitePages, siteRoute } from '../routing.ts';

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
export function localizedPath(path: string, language: Language): string {
  const route = siteRoute(path);
  return sitePages.find((page) => page.pt === route || page.en === route)?.[language].path ?? path;
}
