export function initNavigation() {
  const menu = document.querySelector<HTMLDialogElement>('#mobile-menu');
  const toggle = document.querySelector<HTMLButtonElement>('.menu-toggle');
  if (!menu || !toggle) return;
  const close = () => {
    menu.close();
    toggle.setAttribute('aria-expanded', 'false');
  };
  toggle.addEventListener('click', () => {
    menu.showModal();
    toggle.setAttribute('aria-expanded', 'true');
  });
  menu.querySelector('[data-menu-close]')?.addEventListener('click', close);
  menu.querySelectorAll('a').forEach((link) => link.addEventListener('click', close));
  menu.addEventListener('close', () => toggle.setAttribute('aria-expanded', 'false'));
  menu.addEventListener('click', (event) => {
    if (event.target === menu) close();
  });
  matchMedia('(min-width: 961px)').addEventListener('change', (event) => {
    if (event.matches && menu.open) close();
  });
}
