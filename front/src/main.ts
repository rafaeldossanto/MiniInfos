import './styles/base.css';
import './styles/landing.css';
import './styles/widget.css';
import './styles/language.css';
import './styles/downloads.css';
import './components/usage-widget';
import { project } from './config';
import { initTheme } from './theme/controller';
import { initMotion } from './features/motion';
import { initNavigation } from './features/navigation';
import { initLanguage } from './localization/controller';
import { initDownloads } from './downloads/controller';

document.querySelectorAll<HTMLAnchorElement>('[data-repository]').forEach((link) => {
  link.href = project.repository;
});
document.querySelectorAll<HTMLAnchorElement>('[data-community]').forEach((link) => {
  link.href = project.communityGuide;
});
document.querySelectorAll('[data-year]').forEach((element) => {
  element.textContent = String(new Date().getFullYear());
});
document.querySelectorAll<HTMLButtonElement>('[data-card-theme]').forEach((button) => {
  button.addEventListener('click', () => {
    document
      .querySelector('.personalization-stage usage-widget')
      ?.setAttribute('appearance', button.dataset.cardTheme!);
    document
      .querySelectorAll('[data-card-theme]')
      .forEach((choice) => choice.setAttribute('aria-pressed', String(choice === button)));
  });
});
initLanguage();
initDownloads();
initTheme();
initNavigation();
initMotion();
