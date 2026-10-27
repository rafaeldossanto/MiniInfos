import { revealGeometry, savedTheme, THEME_STORAGE_KEY } from './model';
import type { Theme } from './model';

export function initTheme() {
  const root = document.documentElement;
  const choices = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-theme-choice]'));
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  let running: ViewTransition | undefined;
  let sequence = 0;
  try {
    root.dataset.theme = savedTheme(localStorage);
  } catch {
    /* Storage itself can be unavailable. */
  }
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute('content', root.dataset.theme === 'light' ? '#f4f1ea' : '#181818');

  function apply(theme: Theme) {
    root.dataset.theme = theme;
    choices.forEach((button) =>
      button.setAttribute('aria-pressed', String(button.dataset.themeChoice === theme)),
    );
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute('content', theme === 'light' ? '#f4f1ea' : '#181818');
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      /* The choice still works without storage. */
    }
    const status = document.getElementById('theme-status');
    if (status)
      status.textContent = theme === 'light' ? 'Tema claro ativado.' : 'Tema escuro ativado.';
  }

  choices.forEach((button) => {
    button.setAttribute('aria-pressed', String(button.dataset.themeChoice === root.dataset.theme));
    button.addEventListener('click', async (event) => {
      const next = button.dataset.themeChoice as Theme;
      if (next === root.dataset.theme && !running) return;
      const current = ++sequence;
      running?.skipTransition();
      if (reducedMotion.matches || !document.startViewTransition) {
        apply(next);
        return;
      }

      const bounds = button.getBoundingClientRect();
      const geometry = revealGeometry(
        event.detail
          ? { x: event.clientX, y: event.clientY }
          : { x: bounds.x + bounds.width / 2, y: bounds.y + bounds.height / 2 },
        { width: innerWidth, height: innerHeight },
      );
      root.style.setProperty('--theme-x', geometry.x + 'px');
      root.style.setProperty('--theme-y', geometry.y + 'px');
      root.classList.add('theme-changing');
      const transition = document.startViewTransition(() => {
        if (current === sequence) apply(next);
      });
      running = transition;
      try {
        await transition.ready;
        if (current !== sequence) return;
        const animation = root.animate(
          [{ '--theme-radius': '0px' }, { '--theme-radius': geometry.radius + 'px' }],
          {
            duration: 1200,
            easing: 'cubic-bezier(0.65, 0, 0.35, 1)',
            fill: 'both',
            pseudoElement: '::view-transition-new(root)',
          },
        );
        await animation.finished;
      } catch {
        // Skipped transitions and unsupported pseudo-element animation keep a usable selected theme.
        if (current === sequence) apply(next);
      } finally {
        if (current === sequence) {
          running = undefined;
          root.classList.remove('theme-changing');
        }
      }
    });
  });
}
