import { t } from '../../localization/messages';
import { LANGUAGE_EVENT } from '../../localization/model';
import { project } from '../../config';
import { MAX_ARCHIVE, MAX_FILE, MAX_SOURCE, PackageError, unpack, validateFiles } from './package';
import type { Creation, PackageFiles } from './package';
import { showCreationPreview } from './preview';
import { apiBase, service, SubmissionError } from './api';
import type { Receipt, ServiceConfig } from './api';
import { loadTurnstile } from './turnstile';

const errors: Record<string, string> = {
  archive: 'O pacote ZIP está incompleto ou usa um formato não permitido.',
  size: 'O pacote pode ter até 8 MB compactado, 128 arquivos e 64 MB descompactado.',
  manifest: 'Adicione um manifest.json válido na raiz do pacote.',
  fields: 'O manifesto contém campos não reconhecidos pelo MiniInfos.',
  schema: 'Use schemaVersion 1 e o tipo theme ou mascot.',
  id: 'Use um ID único como autor.minha-criacao, sem espaços.',
  metadata:
    'Preencha nome, autoria e licença. UNLICENSED precisa ser substituído por uma licença real.',
  version: 'Informe uma versão como 1.0.0.',
  definition: 'O manifesto deve definir somente o tipo escolhido.',
  theme: 'Confira o tema base e as dimensões do cabeçalho.',
  colors: 'Use as cores permitidas no formato #RRGGBB ou #AARRGGBB.',
  path: 'Há um caminho de arquivo inválido no pacote.',
  duplicate:
    'Já existe um pacote ou envio com este ID. Escolha outro ID; atualizações passam pelo mantenedor.',
  'file-type': 'Envie somente JSON, PNG, Markdown e texto.',
  png: 'Uma imagem não é um PNG válido.',
  'image-size': 'As imagens podem ter até 8192 pixels por lado e 16 milhões de pixels no total.',
  'missing-image': 'Uma imagem indicada no manifesto não foi encontrada nos arquivos.',
  grid: 'As dimensões do sprite precisam ser divisíveis pelas colunas e linhas da grade.',
  idle: 'Adicione a animação idle para o repouso do mascote.',
  animation: 'Uma animação aponta para uma linha ou quadro inexistente.',
  timing: 'Confira a duração dos quadros: de 16 a 60000 ms e um valor por quadro.',
  anatomy: 'Confira os pontos de interação e os centros de massa do mascote.',
  unavailable:
    'O envio para publicação ainda não está disponível. Você pode preparar, testar e baixar seu pacote aqui.',
  network:
    'Não foi possível confirmar o envio. Confira sua conexão e use Verificar envio antes de tentar novamente.',
  challenge: 'Não foi possível verificar o envio. Refaça a verificação e tente novamente.',
  rate: 'Muitos pedidos em pouco tempo. Aguarde um minuto e tente novamente.',
  request: 'Confira os arquivos e confirme que pode compartilhá-los.',
  missing: 'Este envio não foi encontrado. Confira seu link de acompanhamento.',
};
const labels: Record<Receipt['state'], string> = {
  validating: 'Validando no MiniInfos',
  review: 'Aguardando revisão',
  changes: 'O pacote precisa de ajustes',
  publishing: 'Aprovado, preparando publicação',
  published: 'Publicado na Comunidade',
  rejected: 'Envio encerrado',
  unavailable: 'Envio não concluído',
};
const explanations: Record<Receipt['state'], string> = {
  validating: 'Seu pacote está passando pelo mesmo validador usado pelo aplicativo.',
  review: 'A validação passou. O mantenedor vai conferir a criação antes de publicar.',
  changes:
    'A validação definitiva encontrou problemas. O mantenedor pode consultar os detalhes da revisão.',
  publishing: 'O pacote foi aprovado. Ele aparecerá após a atualização do catálogo e da página.',
  published: 'Sua criação já está disponível no catálogo público.',
  rejected: 'A revisão foi encerrada sem publicação. Você pode preparar uma nova criação.',
  unavailable:
    'Não foi possível concluir o envio. Seus arquivos continuam disponíveis para baixar.',
};
const storageKey = 'miniinfos.community.submissions';
const getHistory = (): string[] => {
  try {
    const v = JSON.parse(localStorage.getItem(storageKey) ?? '{}');
    return v.api === apiBase && Array.isArray(v.ids)
      ? v.ids
          .filter((id: unknown) => typeof id === 'string' && /^[a-f\d-]{36}$/.test(id))
          .slice(0, 10)
      : [];
  } catch {
    return [];
  }
};
const remember = (id: string) => {
  try {
    localStorage.setItem(
      storageKey,
      JSON.stringify({
        api: apiBase,
        ids: [id, ...getHistory().filter((v) => v !== id)].slice(0, 10),
      }),
    );
  } catch {
    /* Tracking link works without storage. */
  }
};

export function initCreator() {
  if (!document.querySelector('.community-page')) return;
  const dialog = document.createElement('dialog');
  dialog.className = 'creation-dialog';
  dialog.setAttribute('aria-labelledby', 'creation-title');
  document.body.append(dialog);
  let creation: Creation | null = null,
    cleanup = () => {},
    config: ServiceConfig = { enabled: false, siteKey: null };
  let ready = false,
    busy = false,
    opener: HTMLElement | null = null,
    challenge = '',
    challengeId: string | null = null;
  let requestId = crypto.randomUUID(),
    receipt: Receipt | null = null,
    timer: ReturnType<typeof setTimeout> | undefined;
  let pickedKind = 'theme',
    message = '',
    lastFiles: PackageFiles | null = null,
    revision = 0;
  let cooldownUntil = 0,
    statusUntil = 0;
  let cooldownTimer: ReturnType<typeof setTimeout> | undefined;
  const secondsLeft = (until: number) => Math.max(0, Math.ceil((until - Date.now()) / 1000));
  const field = (name: string, label: string, max: number, multiline = false) =>
    `<label>${t(label)}${multiline ? `<textarea name="${name}" maxlength="${max}" rows="3"></textarea>` : `<input name="${name}" maxlength="${max}" required ${name === 'id' ? 'pattern="[a-z0-9][a-z0-9._-]{2,79}"' : ''}>`}</label>`;

  function render() {
    cleanup();
    removeChallenge();
    dialog.innerHTML = `<div class="creation-top"><div><span class="creation-eyebrow">${t('Crie e compartilhe')}</span><h2 id="creation-title">${t('Sua próxima criação')}</h2></div><button class="icon-button" type="button" data-close aria-label="${t('Fechar criador')}"><svg aria-hidden="true"><use href="/icons.svg#close"/></svg></button></div>
      <p class="creation-lead">${t('Prepare seu tema ou mascote, veja a prévia e envie para revisão. Tudo por aqui.')}</p>
      <div class="creation-steps" aria-label="${t('Etapas da criação')}"><span>${t('1. Arquivos')}</span><span>${t('2. Prévia e dados')}</span><span>${t('3. Revisão e publicação')}</span></div>
      <form class="creation-form"><div class="creation-layout"><div>
      <fieldset class="creation-kind"><legend>${t('O que você vai criar?')}</legend><label><input type="radio" name="kind" value="theme" ${pickedKind === 'theme' ? 'checked' : ''}>${t('Tema')}</label><label><input type="radio" name="kind" value="mascot" ${pickedKind === 'mascot' ? 'checked' : ''}>${t('Mascote')}</label></fieldset>
      <div class="creation-drop" tabindex="0" role="button" aria-label="${t('Escolher arquivos da criação')}"><svg aria-hidden="true"><use href="/icons.svg#plus"/></svg><strong>${t('Arraste seu pacote ou arquivos aqui')}</strong><span>${t('.miniinfos ou .zip de até 8 MB, ou manifest.json com os PNGs')}</span><span class="community-button">${t('Escolher arquivos')}</span></div>
      <input class="sr-only" type="file" accept=".miniinfos,.zip,.json,.png,.md,.txt" multiple data-files aria-label="${t('Arquivos da criação')}">
      <div class="creation-start"><button type="button" class="community-button" data-new-theme>${t('Começar um tema')}</button><a target="_blank" rel="noopener noreferrer" href="${project.communityGuide}#criar-seu-primeiro-pacote">${t('Guia de arquivos e animações')}</a></div>
      <p class="creation-file-count" data-file-count></p>
      <div class="creation-fields" data-fields hidden>${field('name', 'Nome da criação', 80)}${field('id', 'ID da criação', 80)}${field('author', 'Autoria', 80)}${field('version', 'Versão', 40)}${field('license', 'Licença', 100)}${field('description', 'Descrição', 600, true)}</div>
      <div class="creation-colors" data-colors hidden><label>${t('Fundo do cartão')}<input type="color" data-color="CardBackground" value="#1f1f1f"></label><label>${t('Cor dos destaques')}<input type="color" data-color="Accent" value="#d9cdb8"></label><label>${t('Cor do texto')}<input type="color" data-color="PrimaryText" value="#f1eee8"></label></div>
      </div><aside class="creation-preview-panel"><h3>${t('Sua prévia')}</h3><div data-creation-preview class="creation-preview"><p>${t('Sua criação aparece aqui depois de conferir os arquivos.')}</p></div><p class="creation-preview-note">${t('Prévia com dados de demonstração. A validação definitiva acontece no envio.')}</p></aside></div>
      <p class="creation-feedback" role="status" aria-live="polite" data-feedback></p>
      <label class="creation-rights"><input type="checkbox" required name="rights">${t('Sou autor ou tenho permissão para distribuir estes arquivos e informei os créditos e a licença.')}</label>
      <p class="creation-public-note">${t('Os arquivos, a autoria e a licença enviados ficam públicos na revisão. Não inclua dados pessoais ou segredos.')}</p>
      <div data-challenge></div><div class="creation-actions"><button type="button" class="community-button" data-export disabled>${t('Baixar meu pacote')}</button><button type="submit" class="community-button creation-primary" data-send disabled>${t('Enviar para revisão')}</button></div>
      <p class="creation-service" data-service></p></form>
      <section class="creation-tracking" data-tracking hidden aria-live="polite"><h3>${t('Acompanhar envio')}</h3><p data-tracking-title></p><p data-tracking-detail></p><div class="creation-actions"><button type="button" class="community-button" data-refresh>${t('Verificar envio')}</button><button type="button" class="community-button" data-copy>${t('Copiar link de acompanhamento')}</button></div></section>
      <section class="creation-history" data-history></section>`;
    const form = dialog.querySelector<HTMLFormElement>('form')!;
    const picker = dialog.querySelector<HTMLInputElement>('[data-files]')!;
    const drop = dialog.querySelector<HTMLElement>('.creation-drop')!;
    drop.addEventListener('click', () => {
      if (!busy) picker.click();
    });
    drop.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        if (!busy) picker.click();
      }
    });
    drop.addEventListener('dragover', (e) => {
      e.preventDefault();
      drop.classList.add('dragging');
    });
    drop.addEventListener('dragleave', () => drop.classList.remove('dragging'));
    drop.addEventListener('drop', (e) => {
      e.preventDefault();
      drop.classList.remove('dragging');
      if (!busy && e.dataTransfer?.files) void readFiles([...e.dataTransfer.files]);
    });
    picker.addEventListener('change', () => {
      if (picker.files) void readFiles([...picker.files]);
      picker.value = '';
    });
    form.querySelectorAll<HTMLInputElement>('[name="kind"]').forEach((input) =>
      input.addEventListener('change', () => {
        pickedKind = input.value;
        if (creation && creation.manifest.kind !== pickedKind) {
          creation = null;
          lastFiles = null;
          cleanup();
          render();
        }
      }),
    );
    form
      .querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(
        '[data-fields] input,[data-fields] textarea',
      )
      .forEach((input) => input.addEventListener('change', () => edit(input.name, input.value)));
    form
      .querySelectorAll<HTMLInputElement>('[data-color]')
      .forEach((input) =>
        input.addEventListener('input', () => editColor(input.dataset.color!, input.value)),
      );
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      void submit();
    });
    dialog.querySelector('[data-close]')!.addEventListener('click', () => dialog.close());
    dialog.querySelector('[data-new-theme]')!.addEventListener('click', () => newTheme());
    dialog.querySelector('[data-export]')!.addEventListener('click', () => exportPackage());
    dialog.querySelector('[data-refresh]')!.addEventListener('click', () => {
      if (receipt) void refresh(receipt.id);
    });
    dialog.querySelector('[data-copy]')!.addEventListener('click', () => {
      if (receipt)
        void navigator.clipboard
          .writeText(trackingLink(receipt.id))
          .then(() => feedback('Link copiado. Guarde para acompanhar seu envio.'))
          .catch(() => feedback(trackingLink(receipt!.id)));
    });
    if (creation) fillCreation();
    if (message) feedback(message);
    updateService();
    renderTracking();
    renderHistory();
  }
  function feedback(value: string) {
    message = value;
    dialog.querySelector('[data-feedback]')!.textContent = t(value);
  }
  function error(e: unknown) {
    if (e instanceof SubmissionError && e.retryAfter > 0) {
      cooldownUntil = Date.now() + e.retryAfter * 1000;
      feedback('Aguarde antes de tentar novamente. Seu pacote continua disponível.');
      updateService();
      return;
    }
    feedback(
      errors[e instanceof PackageError || e instanceof SubmissionError ? e.code : 'unavailable'] ??
        errors.unavailable,
    );
  }
  function updateService() {
    dialog
      .querySelectorAll<HTMLInputElement | HTMLTextAreaElement>('input,textarea')
      .forEach((input) => (input.disabled = busy));
    dialog.querySelector<HTMLButtonElement>('[data-close]')!.disabled = busy;
    const wait = secondsLeft(cooldownUntil);
    const canSend =
      ready && config.enabled && Boolean(creation) && !busy && Boolean(challenge) && !wait;
    dialog.querySelector<HTMLButtonElement>('[data-send]')!.disabled = !canSend;
    dialog.querySelector<HTMLButtonElement>('[data-export]')!.disabled = !creation || busy;
    dialog.querySelector('[data-service]')!.textContent = t(
      !ready
        ? 'Conferindo disponibilidade do envio…'
        : config.enabled
          ? 'Após a validação, sua criação passa pela revisão do mantenedor.'
          : errors.unavailable,
    );
    dialog.querySelector('[data-new-theme]')!.toggleAttribute('disabled', busy);
    dialog.querySelector<HTMLButtonElement>('[data-send]')!.textContent = wait
      ? t('Tentar novamente em {seconds}s', { seconds: wait })
      : t(busy ? 'Enviando criação…' : 'Enviar para revisão');
    const refreshButton = dialog.querySelector<HTMLButtonElement>('[data-refresh]')!;
    const statusWait = Math.max(wait, secondsLeft(statusUntil));
    refreshButton.disabled = busy || statusWait > 0;
    refreshButton.textContent = statusWait
      ? t('Verificar em {seconds}s', { seconds: statusWait })
      : t('Verificar envio');
    clearTimeout(cooldownTimer);
    if (dialog.open && (wait || statusWait)) cooldownTimer = setTimeout(updateService, 1000);
  }
  function fillCreation() {
    if (!creation) return;
    dialog.querySelector<HTMLElement>('[data-fields]')!.hidden = false;
    dialog.querySelector('[data-file-count]')!.textContent = t(
      '{count} arquivos conferidos · {size} MB compactado',
      {
        count: Object.keys(creation.files).length,
        size: (creation.archive.length / 1048576).toFixed(2),
      },
    );
    for (const name of ['name', 'id', 'author', 'version', 'license', 'description'] as const) {
      const input = dialog.querySelector<HTMLInputElement>(`[name="${name}"]`)!;
      input.value = creation.manifest[name] ?? '';
    }
    dialog.querySelector<HTMLElement>('[data-colors]')!.hidden = creation.manifest.kind !== 'theme';
    for (const input of dialog.querySelectorAll<HTMLInputElement>('[data-color]')) {
      const c = creation.manifest.theme?.colors?.[input.dataset.color!];
      if (c?.length === 7) input.value = c;
    }
    cleanup();
    cleanup = showCreationPreview(dialog.querySelector('[data-creation-preview]')!, creation);
    updateService();
  }
  async function readFiles(selected: File[]) {
    if (!selected.length) return;
    const current = ++revision;
    busy = true;
    updateService();
    feedback('Conferindo arquivos e preparando a prévia…');
    try {
      if (selected.length > 128 || selected.reduce((n, f) => n + f.size, 0) > MAX_SOURCE)
        throw new PackageError('size');
      let files: PackageFiles = Object.create(null);
      if (selected.length === 1 && /\.(miniinfos|zip)$/i.test(selected[0].name)) {
        if (selected[0].size > MAX_ARCHIVE) throw new PackageError('size');
        files = unpack(new Uint8Array(await selected[0].arrayBuffer()));
      } else
        for (const file of selected) {
          if (file.size > MAX_FILE) throw new PackageError('size');
          const name = file.webkitRelativePath || file.name;
          if (files[name]) throw new PackageError('duplicate');
          files[name] = new Uint8Array(await file.arrayBuffer());
        }
      const next = validateFiles(files);
      // Decode each PNG as well as checking its header before presenting it as a working preview.
      for (const [name, bytes] of Object.entries(next.files))
        if (/\.png$/i.test(name)) {
          try {
            const bitmap = await createImageBitmap(
              new Blob([new Uint8Array(bytes)], { type: 'image/png' }),
            );
            bitmap.close();
          } catch {
            throw new PackageError('png');
          }
        }
      if (current !== revision) return;
      creation = next;
      lastFiles = next.files;
      pickedKind = next.manifest.kind;
      requestId = crypto.randomUUID();
      receipt = null;
      render();
      feedback('Arquivos conferidos. Revise os dados e experimente a prévia.');
      dialog.querySelector<HTMLInputElement>('[name="name"]')!.focus();
    } catch (e) {
      if (current === revision) {
        creation = null;
        lastFiles = null;
        cleanup();
        render();
        error(e);
      }
    } finally {
      if (current === revision) {
        busy = false;
        updateService();
        void setupChallenge();
      }
    }
  }
  function edit(name: string, value: string) {
    if (!lastFiles || busy) return;
    try {
      const data = JSON.parse(new TextDecoder().decode(lastFiles['manifest.json']));
      data[name] = value;
      lastFiles = { ...lastFiles, 'manifest.json': new TextEncoder().encode(JSON.stringify(data)) };
      creation = validateFiles(lastFiles);
      lastFiles = creation.files;
      requestId = crypto.randomUUID();
      feedback('Dados atualizados.');
      fillCreation();
    } catch (e) {
      creation = null;
      updateService();
      error(e);
    }
  }
  function editColor(name: string, value: string) {
    if (!lastFiles || busy) return;
    const data = JSON.parse(new TextDecoder().decode(lastFiles['manifest.json']));
    data.theme.colors = { ...data.theme.colors, [name]: value };
    lastFiles = { ...lastFiles, 'manifest.json': new TextEncoder().encode(JSON.stringify(data)) };
    try {
      creation = validateFiles(lastFiles);
      lastFiles = creation.files;
      requestId = crypto.randomUUID();
      fillCreation();
    } catch (e) {
      creation = null;
      updateService();
      error(e);
    }
  }
  function newTheme() {
    if (busy) return;
    const m = {
      schemaVersion: 1,
      kind: 'theme',
      id: 'autor.meu-tema',
      name: t('Meu tema'),
      author: t('Sua autoria'),
      version: '1.0.0',
      license: 'CC0-1.0',
      description: '',
      theme: {
        base: 'Simple',
        colors: {
          CardBackground: '#1f1f1f',
          PrimaryText: '#f1eee8',
          DimText: '#aaa7a1',
          Accent: '#d9cdb8',
        },
      },
    };
    creation = validateFiles({ 'manifest.json': new TextEncoder().encode(JSON.stringify(m)) });
    lastFiles = creation.files;
    pickedKind = 'theme';
    requestId = crypto.randomUUID();
    receipt = null;
    message = '';
    render();
    void setupChallenge();
    feedback('Ajuste as cores e preencha sua autoria e um ID próprio antes de enviar.');
  }
  function exportPackage() {
    if (!creation) return;
    const url = URL.createObjectURL(
      new Blob([new Uint8Array(creation.archive)], { type: 'application/zip' }),
    );
    const a = document.createElement('a');
    a.href = url;
    a.download = creation.manifest.id + '-' + creation.manifest.version + '.miniinfos';
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  function removeChallenge() {
    if (challengeId) window.turnstile?.remove(challengeId);
    challengeId = null;
    challenge = '';
  }
  async function setupChallenge() {
    if (!dialog.open || !config.enabled || !config.siteKey || challengeId) return;
    try {
      const ts = await loadTurnstile();
      if (!dialog.open || challengeId) return;
      challengeId = ts.render(dialog.querySelector('[data-challenge]')!, {
        sitekey: config.siteKey,
        action: 'community-submit',
        theme: 'auto',
        callback: (value: string) => {
          challenge = value;
          updateService();
        },
        'expired-callback': () => {
          challenge = '';
          updateService();
        },
        'error-callback': () => {
          challenge = '';
          updateService();
          feedback(errors.challenge);
        },
      });
    } catch {
      feedback(errors.challenge);
    }
  }
  async function submit() {
    if (busy || !creation || !config.enabled || !challenge || secondsLeft(cooldownUntil)) return;
    const form = dialog.querySelector<HTMLFormElement>('form')!;
    if (!form.reportValidity()) return;
    if (
      creation.manifest.id === 'autor.meu-tema' ||
      creation.manifest.author === t('Sua autoria')
    ) {
      feedback('Preencha sua autoria e escolha um ID próprio antes de enviar.');
      return;
    }
    busy = true;
    updateService();
    feedback('Enviando os arquivos para validação…');
    remember(requestId);
    try {
      receipt = await service<Receipt>('/submissions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/zip',
          'X-Turnstile-Token': challenge,
          'X-Submission-Id': requestId,
          'X-Rights-Confirmed': 'true',
        },
        body: new Uint8Array(creation.archive),
      });
      remember(receipt.id);
      statusUntil = Date.now() + (receipt.retryAfter ?? 60) * 1000;
      feedback('Envio recebido. Você pode acompanhar por aqui.');
      renderTracking();
      renderHistory();
      schedule();
    } catch (e) {
      error(e);
      receipt = {
        id: requestId,
        packageId: creation.manifest.id,
        version: creation.manifest.version,
        state: 'unavailable',
        createdAt: new Date().toISOString(),
      };
      renderTracking();
    } finally {
      busy = false;
      challenge = '';
      if (challengeId) window.turnstile?.reset(challengeId);
      updateService();
    }
  }
  const trackingLink = (id: string) => location.origin + location.pathname + '#envio=' + id;
  function renderTracking() {
    const section = dialog.querySelector<HTMLElement>('[data-tracking]')!;
    section.hidden = !receipt;
    if (!receipt) return;
    section.querySelector('[data-tracking-title]')!.textContent = t(labels[receipt.state]);
    section.querySelector('[data-tracking-detail]')!.textContent =
      receipt.packageId + ' · ' + t(explanations[receipt.state]);
  }
  function renderHistory() {
    const host = dialog.querySelector('[data-history]')!;
    host.replaceChildren();
    const ids = getHistory();
    if (!ids.length) return;
    const h = document.createElement('h3');
    h.textContent = t('Meus envios neste navegador');
    host.append(h);
    for (const id of ids) {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'community-button';
      b.textContent = t('Acompanhar envio') + ' ' + id.slice(0, 8);
      b.addEventListener('click', () => void refresh(id));
      host.append(b);
    }
  }
  async function refresh(id: string) {
    if (!config.enabled || busy || secondsLeft(cooldownUntil) || secondsLeft(statusUntil)) return;
    clearTimeout(timer);
    busy = true;
    updateService();
    try {
      receipt = await service<Receipt>('/submissions/' + id);
      statusUntil = Date.now() + (receipt.retryAfter ?? 60) * 1000;
      if (receipt.state === 'unavailable') cooldownUntil = Math.max(cooldownUntil, statusUntil);
      remember(id);
      renderTracking();
      renderHistory();
      feedback('Status atualizado.');
      schedule();
    } catch (e) {
      error(e);
    } finally {
      busy = false;
      updateService();
      schedule();
    }
  }
  function schedule() {
    clearTimeout(timer);
    if (
      dialog.open &&
      receipt &&
      !['published', 'rejected', 'changes', 'unavailable'].includes(receipt.state)
    )
      timer = setTimeout(
        () => {
          if (document.visibilityState === 'visible' && receipt) void refresh(receipt.id);
          else schedule();
        },
        Math.max(60000, statusUntil - Date.now(), cooldownUntil - Date.now()),
      );
  }
  async function open(trigger?: HTMLElement) {
    opener = trigger ?? null;
    render();
    dialog.showModal();
    updateService();
    void setupChallenge();
    if (!ready) {
      try {
        config = apiBase
          ? await service<ServiceConfig>('/config')
          : { enabled: false, siteKey: null };
      } catch {
        config = { enabled: false, siteKey: null };
      }
      ready = true;
      updateService();
      void setupChallenge();
    }
    const match = /^#envio=([a-f\d-]{36})$/.exec(location.hash);
    if (match) void refresh(match[1]);
    else schedule();
  }
  document.addEventListener('click', (event) => {
    const target =
      event.target instanceof Element
        ? event.target.closest<HTMLElement>('[data-open-creator]')
        : null;
    if (target) {
      event.preventDefault();
      void open(target);
    }
  });
  dialog.addEventListener('cancel', (e) => {
    if (busy) e.preventDefault();
  });
  dialog.addEventListener('close', () => {
    clearTimeout(timer);
    clearTimeout(cooldownTimer);
    cleanup();
    removeChallenge();
    if (opener?.isConnected) opener.focus();
  });
  window.addEventListener(LANGUAGE_EVENT, () => {
    if (dialog.open) {
      render();
      void setupChallenge();
      schedule();
    }
  });
  window.addEventListener('hashchange', () => {
    if (location.hash === '#enviar' || location.hash.startsWith('#envio=')) void open();
  });
  if (location.hash === '#enviar' || location.hash.startsWith('#envio=')) void open();
}
