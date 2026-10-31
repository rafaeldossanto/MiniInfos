import { pngSize } from './package.ts';
import type { Creation, Track } from './package.ts';
import { t } from '../../localization/messages.ts';

const ease = (value: number) => {
  const p = Math.max(0, Math.min(1, value));
  return (1 - Math.cos(Math.PI * p)) / 2;
};
/** Same frame timing and crossfade convention as CommunityPet.PoseAt, without executing package content. */
export function poseAt(track: Track, columns: number, elapsed: number) {
  const frames = track.frames ?? Array.from({ length: columns }, (_, i) => i);
  const durations = track.durationsMs ?? frames.map(() => track.frameDurationMs ?? 160);
  let time = Math.max(0, elapsed) % durations.reduce((a, b) => a + b, 0),
    index = 0;
  while (index < frames.length - 1 && time >= durations[index]) time -= durations[index++];
  const blend =
    track.blendMs === 0 ? durations[index] : Math.min(track.blendMs ?? 120, durations[index]);
  return {
    frame: frames[index],
    next: frames[(index + 1) % frames.length],
    mix: ease((time - (durations[index] - blend)) / blend),
  };
}
export function showCreationPreview(host: HTMLElement, creation: Creation): () => void {
  host.replaceChildren();
  const urls: string[] = [];
  const url = (name: string) => {
    const value = URL.createObjectURL(
      new Blob([new Uint8Array(creation.files[name])], { type: 'image/png' }),
    );
    urls.push(value);
    return value;
  };
  let raf = 0,
    stopped = false;
  if (creation.manifest.kind === 'theme') {
    const theme = creation.manifest.theme!;
    const widget = document.createElement('usage-widget');
    widget.setAttribute(
      'appearance',
      theme.base === 'Light' ? 'light' : theme.base === 'Classic' ? 'classic-dark' : 'simple',
    );
    const mapped: Record<string, string> = {
      CardBackground: '--widget-bg',
      PrimaryText: '--widget-text',
      DimText: '--widget-muted',
      Divider: '--widget-line',
      Accent: '--widget-accent',
    };
    for (const [key, value] of Object.entries(theme.colors ?? {}))
      if (mapped[key]) widget.style.setProperty(mapped[key], cssColor(value));
    if (theme.headerImage)
      widget.style.setProperty('--widget-art', `url("${url(theme.headerImage)}")`);
    host.append(widget);
    const art = widget.querySelector<HTMLElement>('.widget-art');
    if (art) {
      art.hidden = !theme.headerImage;
      art.style.height = (theme.headerHeight ?? 96) + 'px';
    }
    const header = widget.querySelector<HTMLElement>('.widget-topline');
    if (header) header.style.fontSize = (theme.headerTitleSize ?? 20) + 'px';
    const box = widget.querySelector<HTMLElement>('.usage-widget');
    if (box && theme.colors?.CardBorder) box.style.borderColor = cssColor(theme.colors.CardBorder);
    for (const track of widget.querySelectorAll<HTMLElement>('.widget-track'))
      if (theme.colors?.Track) track.style.background = cssColor(theme.colors.Track);
  } else {
    const mascot = creation.manifest.mascot!;
    const select = document.createElement('select');
    select.setAttribute('aria-label', t('Animação da prévia'));
    const labels: Record<string, string> = {
      idle: 'Repouso',
      active: 'Ativo',
      alert: 'Alerta',
      sleep: 'Dormindo',
      protectEyes: 'Proteger os olhos',
      heldHead: 'Segurado pela cabeça',
      heldLeftWing: 'Segurado pela esquerda',
      heldRightWing: 'Segurado pela direita',
      heldFeet: 'Segurado pelos pés',
      heldBody: 'Segurado pelo corpo',
    };
    for (const state of Object.keys(mascot.states)) {
      const option = document.createElement('option');
      option.value = state;
      option.textContent = t(labels[state]);
      select.append(option);
    }
    const canvas = document.createElement('canvas');
    canvas.width = 320;
    canvas.height = 320;
    canvas.setAttribute('role', 'img');
    canvas.setAttribute('aria-label', t('Prévia do mascote'));
    const controls = document.createElement('div');
    controls.className = 'creation-animation-controls';
    const pause = document.createElement('button');
    pause.type = 'button';
    pause.className = 'community-button';
    let paused = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const label = () => {
      pause.textContent = t(paused ? 'Reproduzir' : 'Pausar');
      pause.setAttribute('aria-pressed', String(paused));
    };
    label();
    controls.append(select, pause);
    host.append(canvas, controls);
    const image = new Image();
    image.src = url(mascot.spriteSheet);
    let elapsed = 0,
      last = 0;
    pause.addEventListener('click', () => {
      paused = !paused;
      label();
    });
    select.addEventListener('change', () => {
      elapsed = 0;
    });
    const draw = (now: number) => {
      if (stopped) return;
      if (!paused) elapsed += last ? Math.min(now - last, 100) : 0;
      last = now;
      const ctx = canvas.getContext('2d');
      if (ctx && image.complete && image.naturalWidth) {
        const [w, h] = pngSize(creation.files[mascot.spriteSheet]);
        const width = w / mascot.columns,
          height = h / mascot.rows;
        const track = mascot.states[select.value];
        const pose = poseAt(track, mascot.columns, elapsed);
        ctx.clearRect(0, 0, 320, 320);
        ctx.globalAlpha = 1 - pose.mix;
        ctx.drawImage(image, pose.frame * width, track.row * height, width, height, 0, 0, 320, 320);
        ctx.globalCompositeOperation = 'lighter';
        ctx.globalAlpha = pose.mix;
        ctx.drawImage(image, pose.next * width, track.row * height, width, height, 0, 0, 320, 320);
        ctx.globalAlpha = 1;
        ctx.globalCompositeOperation = 'source-over';
      }
      raf = requestAnimationFrame(draw);
    };
    raf = requestAnimationFrame(draw);
  }
  return () => {
    stopped = true;
    cancelAnimationFrame(raf);
    for (const u of urls) URL.revokeObjectURL(u);
    host.replaceChildren();
  };
}
/** Packages use WPF #AARRGGBB; CSS uses #RRGGBBAA. */
export function cssColor(value: string): string {
  return value.length === 9 ? '#' + value.slice(3) + value.slice(1, 3) : value;
}
