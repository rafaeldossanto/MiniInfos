type Turnstile = {
  render(host: HTMLElement, options: Record<string, unknown>): string;
  remove(id: string): void;
  reset(id: string): void;
};
declare global {
  interface Window {
    turnstile?: Turnstile;
  }
}
let loading: Promise<Turnstile> | null = null;
export function loadTurnstile(): Promise<Turnstile> {
  if (window.turnstile) return Promise.resolve(window.turnstile);
  if (loading) return loading;
  loading = new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    script.async = true;
    script.defer = true;
    const timer = setTimeout(() => {
      script.remove();
      loading = null;
      reject(new Error('challenge'));
    }, 15000);
    script.onload = () => {
      clearTimeout(timer);
      if (window.turnstile) resolve(window.turnstile);
      else {
        loading = null;
        reject(new Error('challenge'));
      }
    };
    script.onerror = () => {
      clearTimeout(timer);
      script.remove();
      loading = null;
      reject(new Error('challenge'));
    };
    document.head.append(script);
  });
  return loading;
}
