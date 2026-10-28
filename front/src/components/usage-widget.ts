import { t, compactNumber, activeLanguage } from '../localization/messages';
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
const conversationExamples = [
  {
    provider: 'claude',
    title: 'Landing page',
    project: 'MiniInfos',
    ago: 'agora',
    tokens: 5_600_000,
    context: 42,
  },
  {
    provider: 'codex',
    title: 'Ajustes do mascote',
    project: 'MiniInfos',
    ago: 'há 10 min',
    tokens: 3_200_000,
    context: 62,
  },
  {
    provider: 'claude',
    title: 'Tema Clássico',
    project: 'Work',
    ago: 'há 35 min',
    tokens: 1_800_000,
    context: 38,
  },
  {
    provider: 'codex',
    title: 'Revisão de código',
    project: 'Work',
    ago: 'há 1 h',
    tokens: 980_000,
    context: null,
  },
] as const;
const icon = (name: string) =>
  '<svg aria-hidden="true"><use href="/icons.svg#' + name + '"></use></svg>';

/** An interactive demo with fixed sample data; language changes keep its selected provider and appearance. */
class UsageWidget extends HTMLElement {
  private view: View = 'split';
  private filter: Provider | null = null;
  private detailed = false;
  private readonly onLanguageChanged = () => this.render(false);
  private get conversations() {
    return this.getAttribute('card') === 'conversations';
  }

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
      <div class="usage-widget${this.conversations ? ' conversation-widget' : ''}">
        <div class="widget-topline"><span>${this.conversations ? t('Conversas') : 'MiniInfos'}</span><div>
          <button type="button" class="widget-refresh" aria-label="${t('Reiniciar a demonstração')}">${icon('refresh')}</button>
          <span class="widget-close" aria-hidden="true">${icon('close')}</span>
        </div></div>
        <div class="widget-art" aria-hidden="true"></div>
        ${this.conversations ? this.conversationContent() : this.usageContent()}
        <div class="widget-footer">
          <span class="widget-demo-label">${t('Demonstração')}</span>
          <div class="widget-providers" role="group" aria-label="${t(this.conversations ? 'Filtrar conversas por IA' : 'Escolher IA da demonstração')}">
            <button type="button" data-view="claude" aria-label="${t('Mostrar apenas Claude')}">${icon('ai')}</button>
            <button type="button" data-view="codex" aria-label="${t('Mostrar apenas Codex')}">${icon('ai')}</button>
          </div>
          ${
            this.conversations
              ? `
          <div class="widget-number-formats" role="group" aria-label="${t('Formato dos números')}">
            <button type="button" data-number-format="detailed" aria-label="${t('Números detalhados')}" aria-pressed="${this.detailed}"><span aria-hidden="true">☰</span></button>
            <button type="button" data-number-format="compact" aria-label="${t('Números resumidos')}" aria-pressed="${!this.detailed}"><span aria-hidden="true">□</span></button>
          </div>`
              : `
          <button type="button" class="widget-split" data-view="split" aria-label="${t('Mostrar Claude e Codex lado a lado')}">${icon('columns')}</button>`
          }
        </div>
      </div>`;
    this.querySelectorAll<HTMLButtonElement>('[data-view]').forEach((button) => {
      button.addEventListener('click', () => {
        if (!this.conversations) {
          this.selectView(button.dataset.view as View);
          return;
        }
        const provider = button.dataset.view as Provider;
        this.filter = this.filter === provider ? null : provider;
        this.render(false);
        this.querySelector<HTMLButtonElement>(`[data-view="${provider}"]`)?.focus({
          preventScroll: true,
        });
      });
    });
    this.querySelectorAll<HTMLButtonElement>('[data-number-format]').forEach((button) => {
      button.addEventListener('click', () => {
        const format = button.dataset.numberFormat!;
        this.detailed = format === 'detailed';
        this.render(false);
        this.querySelector<HTMLButtonElement>(`[data-number-format="${format}"]`)?.focus({
          preventScroll: true,
        });
      });
    });
    this.querySelector('.widget-refresh')?.addEventListener('click', () => this.reveal());
    if (this.conversations) {
      this.querySelectorAll<HTMLButtonElement>('[data-view]').forEach((button) =>
        button.setAttribute(
          'aria-pressed',
          String(this.filter === null || this.filter === button.dataset.view),
        ),
      );
    } else this.selectView(this.view);
    if (animate) this.reveal();
  }

  private usageContent() {
    return `<div class="widget-columns">${examples
      .map(
        (provider) => `
      <section class="provider" data-provider="${provider.id}" aria-label="${provider.name}">
        <h3>${icon('ai')}${provider.name}</h3>
        ${this.meter('Janela 5h', provider.short, provider.shortTokens, provider.reset)}
        ${this.meter('Semanal', provider.week, provider.weekTokens, provider.weekReset)}
        <div class="widget-totals">
          <div><span>${t('Tokens hoje')}</span><strong>${compactNumber(provider.today)}</strong></div>
          <div><span>${t('Tokens no total')}</span><strong>${compactNumber(provider.total)}</strong></div>
        </div>
        <p class="widget-context">${t('{project} · contexto {percent}%', { project: provider.project, percent: provider.context })}</p>
      </section>`,
      )
      .join('')}</div>`;
  }

  private conversationContent() {
    const rows = conversationExamples.filter(
      (conversation) => !this.filter || conversation.provider === this.filter,
    );
    const count = (value: number) =>
      this.detailed
        ? new Intl.NumberFormat(activeLanguage() === 'pt' ? 'pt-BR' : 'en-US').format(value)
        : compactNumber(value);
    const total = rows.reduce((sum, conversation) => sum + conversation.tokens, 0);
    return `<div class="conversation-body">
      <div class="conversation-heading"><span>${t('Últimas conversas')}</span><span>Tokens</span></div>
      <ul class="conversation-list">${rows
        .map(
          (conversation, index) => `
        <li class="conversation-row" style="--row-delay:${index * 40}ms">
          <span class="conversation-icon">${icon('ai')}</span>
          <div class="conversation-name"><strong>${t(conversation.title)}</strong><span>${conversation.project} · ${t(conversation.ago)}</span></div>
          <div class="conversation-amount"><strong>${count(conversation.tokens)}</strong>${conversation.context === null ? '' : `<span>${t('contexto')} <b>${conversation.context}%</b></span>`}</div>
        </li>`,
        )
        .join('')}</ul>
      <p class="conversation-summary">${t('{count} conversas · {tokens} tokens', { count: rows.length, tokens: count(total) })}</p>
    </div>`;
  }

  /** Tokens and reset are kept whole, so a narrow column breaks the line only between them. */
  private meter(source: string, value: number, tokens: number, reset: string) {
    const label = t(source);
    return `<div class="widget-meter">
      <div><span>${label}</span><b>${value}%</b></div>
      <div class="widget-track" role="meter" aria-label="${label}" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${value}">
        <span style="--fill:${value}%"></span>
      </div><p><span>${compactNumber(tokens)} tokens</span> · <span>${t('reinicia em {time}', { time: reset })}</span></p></div>`;
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
