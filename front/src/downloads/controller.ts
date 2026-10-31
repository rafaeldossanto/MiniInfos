import { t, activeLanguage } from '../localization/messages';
import { LANGUAGE_EVENT, localizedPath } from '../localization/model';
import {
  availablePackages,
  detectPlatform,
  downloadTarget,
  releaseApi,
  releasePage,
} from './model';

export function initDownloads() {
  const section = document.querySelector<HTMLElement>('#downloads');
  const browser = navigator as Navigator & {
    userAgentData?: { platform?: string; mobile?: boolean };
  };
  const detected = detectPlatform(browser.platform, browser.userAgent, {
    platform: browser.userAgentData?.platform,
    mobile: browser.userAgentData?.mobile,
    maxTouchPoints: browser.maxTouchPoints,
  });
  const label =
    detected === 'windows'
      ? 'Baixar para Windows'
      : detected === 'macos'
        ? 'Baixar para macOS'
        : detected === 'linux'
          ? 'Baixar para Linux'
          : 'Baixar MiniInfos';
  const renderNavigation = () => {
    document.querySelectorAll<HTMLAnchorElement>('[data-download]').forEach((link) => {
      link.href = (section ? '' : localizedPath('/', activeLanguage())) + downloadTarget(detected);
      const text = link.querySelector('span');
      if (text) {
        text.dataset.i18n = label;
        text.textContent = t(label);
      }
      const icon = link.querySelector('use');
      if (icon) icon.setAttribute('href', '/icons.svg#file');
    });
    document.querySelectorAll<HTMLElement>('[data-download-recommendation]').forEach((note) => {
      note.hidden = !detected;
      note.textContent = t('Versão recomendada para este computador.');
    });
    document.querySelectorAll<HTMLElement>('[data-download-recommended]').forEach((badge) => {
      badge.hidden = badge.closest<HTMLElement>('[data-platform]')?.dataset.platform !== detected;
      badge.textContent = t('Recomendado para seu sistema');
    });
  };
  renderNavigation();
  window.addEventListener(LANGUAGE_EVENT, renderNavigation);
  if (!section) return;
  section.querySelector(`[data-platform="${detected}"]`)?.setAttribute('data-detected', 'true');
  document
    .querySelector(`[data-platform-shortcut="${detected}"]`)
    ?.setAttribute('data-detected', 'true');
  const links = section.querySelectorAll<HTMLAnchorElement>('[data-package]');
  let availability: Map<string, string> | null = null;
  let failed = false;
  const render = () => {
    document
      .querySelectorAll<HTMLAnchorElement>('[data-download] span')
      .forEach((text) => (text.textContent = t(label)));
    links.forEach((link) => {
      const address = availability?.get(link.dataset.package!);
      link.href = address ?? releasePage;
      const platform = link.closest('[data-platform]')?.querySelector('h3')?.textContent;
      const stateText = t(
        address
          ? 'Baixar'
          : failed
            ? 'Ver releases'
            : availability
              ? 'Ainda não publicado'
              : 'Verificando…',
      );
      link.setAttribute(
        'aria-label',
        `${platform} · ${link.querySelector('span')?.textContent}: ${stateText}`,
      );
      link.dataset.available = String(Boolean(address));
      const state = link.querySelector('[data-download-state]');
      if (state) state.textContent = stateText;
    });
    const status = section.querySelector('[data-download-status]');
    if (status)
      status.textContent = t(
        failed
          ? 'Não foi possível consultar as versões. Confira os arquivos publicados no GitHub.'
          : 'Os downloads são ativados conforme os pacotes são publicados no GitHub.',
      );
  };
  window.addEventListener(LANGUAGE_EVENT, render);
  render();
  // Fetch only when the download area is approached or a main download CTA is used.
  let requested = false;
  const load = async () => {
    if (requested) return;
    requested = true;
    try {
      const response = await fetch(releaseApi, {
        headers: { Accept: 'application/vnd.github+json' },
        signal: AbortSignal.timeout(8000),
      });
      if (!response.ok) throw new Error('Release unavailable');
      availability = availablePackages(await response.json());
    } catch {
      failed = true;
    }
    render();
  };
  document
    .querySelectorAll('[data-download], [data-platform-shortcut]')
    .forEach((link) => link.addEventListener('click', () => void load()));
  const observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) {
        void load();
        observer.disconnect();
      }
    },
    { rootMargin: '240px' },
  );
  observer.observe(section);
}
