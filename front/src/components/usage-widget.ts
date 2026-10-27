import { t, compactNumber } from '../localization/messages';
import { LANGUAGE_EVENT } from '../localization/model';

type Provider = 'claude' | 'codex';
type View = Provider | 'split';
const examples = [
  {
    id: 'claude',
    name: 'Claude',
    short: 51,
    week: 9,
    shortTokens: 69_100_000,
    weekTokens: 75_400_000,
    reset: '3h 8min',
    weekReset: '6d 19h',
    today: 128_000_000,
    total: 3_900_000_000,
    project: 'MiniInfos',
    context: 24,
  },
  {
    id: 'codex',
    name: 'Codex (GPT)',
    short: 27,
    week: 19,
    shortTokens: 13_900_000,
    weekTokens: 46_500_000,
    reset: '3h 42min',
    weekReset: '6d 17h',
    today: 46_500_000,
    total: 46_500_000,
    project: 'Work',
    context: 78,
  },
] as const;
const icon = (name: string) =>
  '<svg aria-hidden="true"><use href="/icons.svg#' + name + '"></use></svg>';

/** An interactive demo with fixed sample data; language changes keep its selected provider and appearance. */
class UsageWidget extends HTMLElement {
  private view: View = 'split';
  private readonly onLanguageChanged = () => this.render(false);

  connectedCallback() {
    window.addEventListener(LANGUAGE_EVENT, this.onLanguageChanged);
    if (this.dataset.initialized) return;
    this.dataset.initialized = 'true';
    this.render(true);
  }

  disconnectedCallback() {
    window.removeEventListener(LANGUAGE_EVENT, this.onLanguageChanged);
  }

  private render(animate: boolean) {
    this.innerHTML = `
      <div class="usage-widget">
        <div class="widget-topline"><span>MiniInfos</span><div>
          <button type="button" class="widget-refresh" aria-label="${t('Reiniciar a demonstração')}">${icon('refresh')}</button>
          <span class="widget-close" aria-hidden="true">${icon('close')}</span>
        </div></div>
        <div class="widget-art" aria-hidden="true"></div>
        <div class="widget-columns">${examples
          .map(
            (provider) => `
          <section class="provider" data-provider="${provider.id}" aria-label="${provider.name}">
            <h3>${icon('ai')}${provider.name}</h3>
            ${this.meter('Janela 5h', provider.short, compactNumber(provider.shortTokens) + ' tokens · ' + t('reinicia em {time}', { time: provider.reset }))}
            ${this.meter('Semanal', provider.week, compactNumber(provider.weekTokens) + ' tokens · ' + t('reinicia em {time}', { time: provider.weekReset }))}
            <div class="widget-totals">
              <div><span>${t('Tokens hoje')}</span><strong>${compactNumber(provider.today)}</strong></div>
              <div><span>${t('Tokens no total')}</span><strong>${compactNumber(provider.total)}</strong></div>
            </div>
            <p class="widget-context">${t('{project} · contexto {percent}%', { project: provider.project, percent: provider.context })}</p>
          </section>`,
          )
          .join('')}
        </div>
        <div class="widget-footer">
          <span class="widget-demo-label">${t('Demonstração')}</span>
          <div class="widget-providers" role="group" aria-label="${t('Escolher IA da demonstração')}">
            <button type="button" data-view="claude" aria-label="${t('Mostrar apenas Claude')}">${icon('ai')}</button>
            <button type="button" data-view="codex" aria-label="${t('Mostrar apenas Codex')}">${icon('ai')}</button>
          </div>
          <button type="button" class="widget-split" data-view="split" aria-label="${t('Mostrar Claude e Codex lado a lado')}">${icon('columns')}</button>
        </div>
      </div>`;
    this.querySelectorAll<HTMLButtonElement>('[data-view]').forEach((button) => {
      button.addEventListener('click', () => this.selectView(button.dataset.view as View));
    });
    this.querySelector('.widget-refresh')?.addEventListener('click', () => this.reveal());
    this.selectView(this.view);
    if (animate) this.reveal();
  }

  private meter(source: string, value: number, detail: string) {
    const label = t(source);
    return `<div class="widget-meter">
      <div><span>${label}</span><b>${value}%</b></div>
      <div class="widget-track" role="meter" aria-label="${label}" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${value}">
        <span style="--fill:${value}%"></span>
      </div><p>${detail}</p></div>`;
  }

  private selectView(view: View) {
    this.view = view;
    this.dataset.view = view;
    this.querySelectorAll<HTMLElement>('[data-provider]').forEach((column) => {
      column.hidden = view !== 'split' && column.dataset.provider !== view;
    });
    this.querySelectorAll<HTMLButtonElement>('[data-view]').forEach((button) => {
      button.setAttribute(
        'aria-pressed',
        String(button.dataset.view === this.view || this.view === 'split'),
      );
    });
  }

  private reveal() {
    this.classList.remove('widget-ready');
    requestAnimationFrame(() => requestAnimationFrame(() => this.classList.add('widget-ready')));
  }
}

customElements.define('usage-widget', UsageWidget);
