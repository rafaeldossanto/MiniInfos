import { activeLanguage, t } from '../localization/messages';
import { LANGUAGE_EVENT, localizedPath } from '../localization/model';
import {
  assetUrl,
  catalogRelease,
  galleryItems,
  readCatalog,
  selectItems,
  MAX_INDEX_BYTES,
} from './model';
import type { Catalog, CatalogFilter, CatalogSort, GalleryItem } from './model';

function element<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className = '',
  text?: string,
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}
function icon(name: string): SVGSVGElement {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('aria-hidden', 'true');
  const use = document.createElementNS(svg.namespaceURI, 'use');
  use.setAttribute('href', '/icons.svg#' + name);
  svg.append(use);
  return svg;
}
const nameOf = (item: GalleryItem) => (item.published ? item.name : t(item.name));
const descriptionOf = (item: GalleryItem) =>
  item.published ? item.description : t(item.description);

export function initCommunity() {
  const root = document.querySelector<HTMLElement>('.community-catalog');
  if (!root) return;
  const results = root.querySelector<HTMLElement>('[data-catalog-results]')!;
  const search = root.querySelector<HTMLInputElement>('[data-catalog-search]')!;
  const sortControl = root.querySelector<HTMLSelectElement>('[data-catalog-sort]')!;
  const empty = root.querySelector<HTMLElement>('[data-catalog-empty]')!;
  const status = root.querySelector<HTMLElement>('[data-catalog-status]')!;
  const retry = root.querySelector<HTMLButtonElement>('[data-catalog-retry]')!;
  const dialog = document.querySelector<HTMLDialogElement>('[data-package-dialog]')!;
  let catalog: Catalog | null = null;
  let filter: CatalogFilter = 'all';
  let sort: CatalogSort = 'featured';
  let loading = false;
  let failed = false;
  let expanded = 12;
  let selected: GalleryItem | null = null;
  let opener: HTMLElement | null = null;
  const previews = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        const node = entry.target as HTMLElement;
        if (!(node instanceof HTMLImageElement)) {
          node.classList.toggle('preview-visible', entry.isIntersecting);
          continue;
        }
        if (!entry.isIntersecting) continue;
        if (node instanceof HTMLImageElement && node.dataset.preview) {
          node.src = node.dataset.preview;
          delete node.dataset.preview;
        }
        node.classList.add('preview-visible');
        previews.unobserve(node);
      }
    },
    { rootMargin: '100px' },
  );

  function preview(item: GalleryItem): HTMLElement {
    const box = element('div', 'catalog-preview');
    if (item.appearance) {
      box.classList.add('catalog-preview-theme');
      const widget = document.createElement('usage-widget');
      widget.setAttribute('appearance', item.appearance);
      widget.setAttribute('compact', '');
      widget.setAttribute('aria-label', t('Demonstração do tema {name}', { name: nameOf(item) }));
      box.append(widget);
    } else if (item.owl) {
      const owl = element('div', 'pet-sprite catalog-owl');
      owl.setAttribute('role', 'img');
      owl.setAttribute('aria-label', t('Coruja de Atena'));
      box.append(owl);
      previews.observe(owl);
    } else if (item.published) {
      box.classList.add(
        item.kind === 'mascot' ? 'catalog-preview-mascot' : 'catalog-preview-published-theme',
      );
      const image = element('img');
      image.alt = t('Prévia de {name}', { name: item.name });
      image.width = 128;
      image.height = 128;
      image.decoding = 'async';
      image.referrerPolicy = 'no-referrer';
      image.dataset.preview = assetUrl(item.published.preview);
      image.addEventListener(
        'error',
        () => {
          image.hidden = true;
          box.append(element('span', 'preview-unavailable', t('Prévia indisponível')));
        },
        { once: true },
      );
      box.append(image);
      previews.observe(image);
    }
    return box;
  }

  function download(item: GalleryItem): HTMLAnchorElement {
    const link = element('a', 'catalog-download');
    link.href = assetUrl(item.published!.asset);
    link.setAttribute('aria-label', t('Baixar {name}', { name: item.name }));
    link.title = t('Baixar pacote .miniinfos');
    link.append(icon('download'));
    return link;
  }

  function card(item: GalleryItem, index: number): HTMLElement {
    const article = element('article', 'catalog-card');
    article.dataset.packageId = item.id;
    article.style.setProperty('--entry-delay', Math.min(index, 5) * 40 + 'ms');
    article.append(preview(item));
    const heading = element('div', 'catalog-card-heading');
    heading.append(
      element('h3', '', nameOf(item)),
      element('span', 'catalog-badge', t(item.published ? 'Comunidade' : 'Incluído no app')),
    );
    article.append(heading, element('p', 'catalog-author', item.author));
    const actions = element('div', 'catalog-card-actions');
    const details = element('button', 'community-button');
    details.type = 'button';
    details.append(element('span', '', t('Ver detalhes')), icon('arrow-right'));
    details.setAttribute('aria-label', t('Ver detalhes de {name}', { name: nameOf(item) }));
    details.addEventListener('click', () => {
      selected = item;
      opener = details;
      renderDialog(item);
      dialog.showModal();
    });
    actions.append(details);
    if (item.published) actions.append(download(item));
    article.append(actions);
    return article;
  }

  function contribute(): HTMLElement {
    const article = element('article', 'catalog-card catalog-contribute');
    // Original neutral outline: no face, costume, character art or company mark.
    article.innerHTML =
      '<svg class="contribute-silhouette" aria-hidden="true" viewBox="0 0 140 160"><path d="M37 12C16 14 14 27 14 54v15c0 18 11 24 30 26l-9 13-7 29 13 4 8-20-2 28 18 1 5-20 5 20 18-1-2-28 8 20 13-4-7-29-9-13c19-2 30-8 30-26V54c0-27-2-40-23-42Z"/><path class="contribute-plus" d="M70 49v24M58 61h24"/></svg>';
    article.append(
      element('h3', '', t('Seu próximo mascote')),
      element('p', '', t('Crie e compartilhe com a comunidade.')),
    );
    const link = element('a', 'community-button', t('Enviar criação'));
    link.href = '#enviar';
    link.dataset.openCreator = '';
    link.append(icon('arrow-right'));
    article.append(link);
    return article;
  }

  function render() {
    previews.disconnect();
    const items = galleryItems(catalog);
    // Translate only our own built-in metadata. Community authors control their original text.
    const localized = items.map((item) =>
      item.published ? item : { ...item, name: t(item.name), description: t(item.description) },
    );
    const matches = selectItems(localized, filter, search.value, sort, activeLanguage());
    const fragment = document.createDocumentFragment();
    for (const kind of ['theme', 'mascot'] as const) {
      const group = matches.filter((item) => item.kind === kind);
      if (!group.length) continue;
      const section = element('section', 'catalog-section');
      const heading = element('div', 'catalog-section-heading');
      const title = element('h2');
      title.append(
        icon(kind === 'theme' ? 'palette' : 'pet'),
        document.createTextNode(t(kind === 'theme' ? 'Temas' : 'Mascotes')),
      );
      heading.append(title);
      const limit = filter === 'all' && !search.value.trim() ? 3 : expanded;
      if (group.length > limit) {
        const more = element(
          'button',
          'catalog-see-all',
          t(filter === 'all' ? 'Ver todos' : 'Mostrar mais'),
        );
        more.type = 'button';
        more.append(icon('arrow-right'));
        more.addEventListener('click', () => {
          if (filter === 'all') {
            filter = kind;
            updateFilters();
            root!
              .querySelector<HTMLButtonElement>(`[data-catalog-filter="${kind}"]`)
              ?.focus({ preventScroll: true });
          } else expanded += 12;
          render();
        });
        heading.append(more);
      }
      const grid = element('div', 'catalog-grid');
      grid.append(
        ...group.slice(0, limit).map((item, index) =>
          card(
            items.find((original) => original.id === item.id)!,
            index,
          ),
        ),
      );
      if (kind === 'mascot' && !search.value.trim()) grid.append(contribute());
      section.append(heading, grid);
      fragment.append(section);
    }
    results.replaceChildren(fragment);
    empty.hidden = matches.length > 0;
    retry.hidden = !failed;
    status.textContent = loading
      ? t('Carregando pacotes da comunidade…')
      : failed
        ? t(
            'Não foi possível carregar os pacotes da comunidade. Os itens incluídos no app continuam disponíveis.',
          )
        : t('{count} pacotes publicados · os itens incluídos já vêm no MiniInfos.', {
            count: catalog?.packages.length ?? 0,
          });
    if (selected && dialog.open) renderDialog(selected);
  }

  function renderDialog(item: GalleryItem) {
    dialog.querySelector('[data-dialog-kind]')!.textContent = t(
      item.kind === 'theme' ? 'Tema' : 'Mascote',
    );
    dialog.querySelector('[data-dialog-preview]')!.replaceChildren(preview(item));
    dialog.querySelector('#package-dialog-title')!.textContent = nameOf(item);
    dialog.querySelector('[data-dialog-author]')!.textContent = item.author;
    dialog.querySelector('[data-dialog-description]')!.textContent = descriptionOf(item);
    const metadata = dialog.querySelector('[data-dialog-metadata]')!;
    metadata.replaceChildren();
    if (item.published) {
      const pairs = [
        [t('Versão'), item.published.version],
        [t('Licença'), item.published.license],
        [
          t('Tamanho'),
          new Intl.NumberFormat(activeLanguage(), { maximumFractionDigits: 1 }).format(
            item.published.size / 1024,
          ) + ' KB',
        ],
      ];
      for (const [key, value] of pairs)
        metadata.append(element('dt', '', key), element('dd', '', value));
    }
    dialog.querySelector('[data-dialog-help]')!.textContent = item.published
      ? t(
          'No MiniInfos, abra Configurações e escolha Adicionar tema ou Adicionar mascote. Depois, selecione o arquivo .miniinfos que você baixou.',
        )
      : t(
          item.kind === 'theme'
            ? 'Este tema já está no MiniInfos. Abra Configurações → Aparência e selecione o tema.'
            : 'Este mascote já está no MiniInfos. Abra Configurações → Mascote e selecione a Coruja de Atena.',
        );
    const actions = dialog.querySelector('[data-dialog-actions]')!;
    actions.replaceChildren();
    const link = element(
      'a',
      'community-button',
      t(item.published ? 'Baixar pacote .miniinfos' : 'Baixar MiniInfos'),
    );
    link.href = item.published
      ? assetUrl(item.published.asset)
      : localizedPath('/', activeLanguage()) + '#downloads';
    link.append(icon(item.published ? 'download' : 'arrow-right'));
    actions.append(link);
    if (item.published) {
      const source = element('a', 'catalog-source', t('Ver no GitHub'));
      source.href = catalogRelease;
      source.target = '_blank';
      source.rel = 'noopener noreferrer';
      actions.append(source);
    }
  }

  function updateFilters() {
    root!
      .querySelectorAll<HTMLButtonElement>('[data-catalog-filter]')
      .forEach((button) =>
        button.setAttribute('aria-pressed', String(button.dataset.catalogFilter === filter)),
      );
  }
  async function load() {
    if (loading) return;
    loading = true;
    failed = false;
    render();
    try {
      const response = await fetch('/community/catalog.json', {
        signal: AbortSignal.timeout(8000),
        cache: 'no-cache',
      });
      if (!response.ok || Number(response.headers.get('content-length')) > MAX_INDEX_BYTES)
        throw new Error('Catalog unavailable');
      const body = await response.text();
      if (new TextEncoder().encode(body).byteLength > MAX_INDEX_BYTES)
        throw new Error('Catalog too large');
      catalog = readCatalog(JSON.parse(body));
    } catch {
      failed = true;
    } finally {
      loading = false;
      render();
    }
  }
  root.querySelectorAll<HTMLButtonElement>('[data-catalog-filter]').forEach((button) =>
    button.addEventListener('click', () => {
      filter = button.dataset.catalogFilter as CatalogFilter;
      expanded = 12;
      updateFilters();
      render();
    }),
  );
  search.addEventListener('input', () => {
    expanded = 12;
    render();
  });
  sortControl.addEventListener('change', () => {
    sort = sortControl.value as CatalogSort;
    render();
  });
  root.querySelector('[data-catalog-clear]')!.addEventListener('click', () => {
    search.value = '';
    filter = 'all';
    updateFilters();
    render();
    search.focus();
  });
  retry.addEventListener('click', () => void load());
  dialog.querySelector('[data-dialog-close]')!.addEventListener('click', () => dialog.close());
  dialog.addEventListener('click', (event) => {
    if (event.target === dialog) {
      const box = dialog.getBoundingClientRect();
      if (
        event.clientX < box.left ||
        event.clientX > box.right ||
        event.clientY < box.top ||
        event.clientY > box.bottom
      )
        dialog.close();
    }
  });
  dialog.addEventListener('close', () => {
    const id = selected?.id;
    selected = null;
    const target = opener?.isConnected
      ? opener
      : results.querySelector<HTMLElement>(`[data-package-id="${id}"] .community-button`);
    (target ?? search).focus({ preventScroll: true });
  });
  window.addEventListener(LANGUAGE_EVENT, render);
  void load();
}
