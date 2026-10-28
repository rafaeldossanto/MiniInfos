import { LANGUAGE_EVENT } from '../localization/model';
import { t } from '../localization/messages';
import { swipeStep } from './carousel-gesture';

type Drag = { id: number; startX: number; startY: number; x: number; y: number; active: boolean };

/** One preview slot, navigable by buttons, keyboard or a horizontal pointer gesture. */
class CardCarousel extends HTMLElement {
  private selected = 0;
  private slides: HTMLElement[] = [];
  private viewport!: HTMLElement;
  private drag: Drag | null = null;
  private events?: AbortController;
  private readonly onLanguageChanged = () => this.updateNavigation();

  connectedCallback() {
    this.slides = [...this.querySelectorAll<HTMLElement>('.card-carousel-slide')];
    this.viewport = this.querySelector<HTMLElement>('.card-carousel-viewport')!;
    this.events = new AbortController();
    const options = { signal: this.events.signal };
    this.querySelector('[data-carousel-previous]')?.addEventListener(
      'click',
      () => this.select(this.selected - 1),
      options,
    );
    this.querySelector('[data-carousel-next]')?.addEventListener(
      'click',
      () => this.select(this.selected + 1),
      options,
    );
    this.addEventListener(
      'keydown',
      (event) => {
        if (
          event.altKey ||
          event.ctrlKey ||
          event.metaKey ||
          (event.target as Element).closest('select, input, textarea')
        )
          return;
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        event.preventDefault();
        this.select(this.selected + (event.key === 'ArrowRight' ? 1 : -1));
      },
      options,
    );
    this.viewport.addEventListener(
      'pointerdown',
      (event) => {
        if (
          !event.isPrimary ||
          event.button !== 0 ||
          (event.target as Element).closest('button, a, input, select, textarea')
        )
          return;
        this.drag = {
          id: event.pointerId,
          startX: event.clientX,
          startY: event.clientY,
          x: 0,
          y: 0,
          active: false,
        };
      },
      options,
    );
    this.viewport.addEventListener('pointermove', (event) => this.move(event), options);
    this.viewport.addEventListener(
      'pointerup',
      (event) => {
        if (event.pointerId === this.drag?.id) this.finish(true);
      },
      options,
    );
    this.viewport.addEventListener(
      'pointercancel',
      (event) => {
        if (event.pointerId === this.drag?.id) this.finish(false);
      },
      options,
    );
    this.viewport.addEventListener(
      'lostpointercapture',
      (event) => {
        if (event.pointerId === this.drag?.id) this.finish(false);
      },
      options,
    );
    window.addEventListener('blur', () => this.finish(false), options);
    window.addEventListener(LANGUAGE_EVENT, this.onLanguageChanged);
    this.select(this.selected);
  }

  disconnectedCallback() {
    this.finish(false);
    this.events?.abort();
    window.removeEventListener(LANGUAGE_EVENT, this.onLanguageChanged);
  }

  private select(index: number) {
    this.selected = Math.max(0, Math.min(this.slides.length - 1, index));
    this.style.setProperty('--card-index', String(this.selected));
    this.slides.forEach((slide, position) => {
      slide.inert = position !== this.selected;
      slide.setAttribute('aria-hidden', String(slide.inert));
    });
    this.updateNavigation();
    const focused = document.activeElement;
    if (
      focused instanceof HTMLElement &&
      this.contains(focused) &&
      (focused.closest('[inert]') || (focused instanceof HTMLButtonElement && focused.disabled))
    ) {
      this.focus({ preventScroll: true });
    }
  }

  private updateNavigation() {
    const label = this.querySelector<HTMLElement>('[data-carousel-label]');
    if (label) {
      label.dataset.i18n = this.selected === 0 ? 'Uso' : 'Conversas';
      label.textContent = t(label.dataset.i18n);
    }
    this.querySelector<HTMLButtonElement>('[data-carousel-previous]')!.disabled =
      this.selected === 0;
    this.querySelector<HTMLButtonElement>('[data-carousel-next]')!.disabled =
      this.selected === this.slides.length - 1;
  }

  private move(event: PointerEvent) {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.id) return;
    if (event.pointerType === 'mouse' && event.buttons !== 1) {
      this.finish(false);
      return;
    }
    drag.x = event.clientX - drag.startX;
    drag.y = event.clientY - drag.startY;
    if (!drag.active) {
      if (Math.abs(drag.y) > 8 && Math.abs(drag.y) >= Math.abs(drag.x)) {
        this.finish(false);
        return;
      }
      if (Math.abs(drag.x) < 8) return;
      drag.active = true;
      this.viewport.setPointerCapture(drag.id);
      this.classList.add('is-dragging');
    }
    const pastEdge =
      (this.selected === 0 && drag.x > 0) ||
      (this.selected === this.slides.length - 1 && drag.x < 0);
    const offset = pastEdge
      ? drag.x / 4
      : Math.max(-this.viewport.clientWidth, Math.min(this.viewport.clientWidth, drag.x));
    this.style.setProperty('--card-drag', `${offset}px`);
  }

  private finish(commit: boolean) {
    const drag = this.drag;
    this.drag = null;
    if (drag?.active && this.viewport.hasPointerCapture(drag.id))
      this.viewport.releasePointerCapture(drag.id);
    this.classList.remove('is-dragging');
    this.style.removeProperty('--card-drag');
    if (commit && drag?.active)
      this.select(this.selected + swipeStep(drag.x, drag.y, this.viewport.clientWidth));
  }
}

customElements.define('card-carousel', CardCarousel);
