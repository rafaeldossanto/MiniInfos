import {
  languageChoice,
  savedLanguage,
  resolveLanguage,
  localizedPath,
  socialImage,
  LANGUAGE_STORAGE_KEY,
  LANGUAGE_EVENT,
} from './model';
import type { Language, LanguageChoice } from './model';
import { setActiveLanguage, t } from './messages';

export function initLanguage() {
  const controls = document.querySelectorAll<HTMLSelectElement>('[data-language-choice]');
  let choice: LanguageChoice = 'auto';
  try {
    choice = savedLanguage(localStorage);
  } catch {
    /* Browser storage can be unavailable. */
  }
  const browsers = () => (navigator.languages?.length ? navigator.languages : [navigator.language]);
  function apply(language: Language) {
    setActiveLanguage(language);
    document.documentElement.lang = language === 'pt' ? 'pt-BR' : 'en';
    document.querySelectorAll<HTMLElement>('[data-i18n]').forEach((element) => {
      element.textContent = t(element.dataset.i18n!);
    });
    for (const attribute of ['aria-label', 'alt', 'title', 'content', 'placeholder']) {
      document.querySelectorAll<HTMLElement>('[data-i18n-' + attribute + ']').forEach((element) => {
        element.setAttribute(attribute, t(element.getAttribute('data-i18n-' + attribute)!));
      });
    }
    controls.forEach((control) => {
      control.value = choice;
    });
    document.querySelectorAll<HTMLAnchorElement>('a[href]').forEach((link) => {
      const href = link.getAttribute('href')!;
      if (href.startsWith('/') && !href.startsWith('//'))
        link.setAttribute('href', localizedPath(href, language));
    });
    const path = localizedPath(location.pathname, language);
    if (path !== location.pathname)
      history.replaceState(null, '', path + location.search + location.hash);
    const canonical = document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (canonical) canonical.href = new URL(path, 'https://miniinfos.com').href;
    document
      .querySelector('meta[property="og:url"]')
      ?.setAttribute('content', new URL(path, 'https://miniinfos.com').href);
    document
      .querySelector('meta[property="og:image"]')
      ?.setAttribute('content', new URL(socialImage(language), 'https://miniinfos.com').href);
    window.dispatchEvent(new Event(LANGUAGE_EVENT));
  }
  controls.forEach((control) =>
    control.addEventListener('change', () => {
      choice = languageChoice(control.value);
      try {
        localStorage.setItem(LANGUAGE_STORAGE_KEY, choice);
      } catch {
        /* The current visit still works. */
      }
      apply(resolveLanguage(choice, browsers()));
    }),
  );
  window.addEventListener('languagechange', () => {
    if (choice === 'auto') apply(resolveLanguage(choice, browsers()));
  });
  window.addEventListener('storage', (event) => {
    if (event.key !== LANGUAGE_STORAGE_KEY) return;
    choice = languageChoice(event.newValue);
    apply(resolveLanguage(choice, browsers()));
  });
  // An explicit English URL is readable without storage or JS and remains English on its first visit.
  apply(
    choice === 'auto' && location.pathname.startsWith('/en/')
      ? 'en'
      : resolveLanguage(choice, browsers()),
  );
}
