import english from './en.json' with { type: 'json' };
import type { Language } from './model';

let language: Language =
  typeof document !== 'undefined' && document.documentElement.lang.startsWith('en') ? 'en' : 'pt';
export function activeLanguage() {
  return language;
}
export function setActiveLanguage(next: Language) {
  language = next;
}
export function t(source: string, values: Record<string, string | number> = {}): string {
  const message =
    language === 'en' ? ((english as Record<string, string>)[source] ?? source) : source;
  return message.replace(/\{(\w+)\}/g, (placeholder, key: string) =>
    String(values[key] ?? placeholder),
  );
}
export function compactNumber(value: number): string {
  const unit = value >= 1e9 ? 'B' : value >= 1e6 ? 'M' : value >= 1e3 ? 'k' : '';
  const scale = value >= 1e9 ? 1e9 : value >= 1e6 ? 1e6 : value >= 1e3 ? 1e3 : 1;
  return (
    new Intl.NumberFormat(language === 'pt' ? 'pt-BR' : 'en-US', {
      maximumFractionDigits: 1,
    }).format(value / scale) + unit
  );
}
