import { initSite } from './site';
import './components/usage-widget';
import './components/card-carousel';
import { initMotion } from './features/motion';
document.querySelectorAll<HTMLButtonElement>('[data-card-theme]').forEach((button) => {
  button.addEventListener('click', () => {
    document
      .querySelectorAll('.personalization-stage usage-widget')
      .forEach((card) => card.setAttribute('appearance', button.dataset.cardTheme!));
    document
      .querySelectorAll('[data-card-theme]')
      .forEach((choice) => choice.setAttribute('aria-pressed', String(choice === button)));
  });
});
initSite();
initMotion();
