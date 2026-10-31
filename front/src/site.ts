import { project } from './config';
import { initTheme } from './theme/controller';
import { initNavigation } from './features/navigation';
import { initLanguage } from './localization/controller';
import { initDownloads } from './downloads/controller';

export function initSite() {
  document
    .querySelectorAll<HTMLAnchorElement>('[data-repository]')
    .forEach((link) => (link.href = project.repository));
  document
    .querySelectorAll<HTMLAnchorElement>('[data-community]')
    .forEach((link) => (link.href = project.communityPage));
  document
    .querySelectorAll('[data-year]')
    .forEach((element) => (element.textContent = String(new Date().getFullYear())));
  initLanguage();
  initDownloads();
  initTheme();
  initNavigation();
}
