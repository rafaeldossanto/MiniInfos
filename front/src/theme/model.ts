export type Theme = 'dark' | 'light';
export const THEME_STORAGE_KEY = 'miniinfos.site.theme';

export function savedTheme(storage: Pick<Storage, 'getItem'>): Theme {
  try {
    return storage.getItem(THEME_STORAGE_KEY) === 'light' ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}

export function revealGeometry(
  point: { x: number; y: number },
  viewport: { width: number; height: number },
) {
  const x = Math.max(0, Math.min(point.x, viewport.width));
  const y = Math.max(0, Math.min(point.y, viewport.height));
  return {
    x,
    y,
    radius: Math.hypot(Math.max(x, viewport.width - x), Math.max(y, viewport.height - y)) + 32,
  };
}
