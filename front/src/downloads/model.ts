import { project } from '../config.ts';

export type Platform = 'windows' | 'macos' | 'linux';
export const releaseApi = `https://api.github.com/repositories/${project.repositoryId}/releases/latest`;
export const releasePage = 'https://github.com/rafaeldossanto/MiniInfos/releases/latest';
export const packages = [
  { platform: 'windows', file: 'MiniInfos-Setup.exe', label: 'Windows · x64' },
  { platform: 'macos', file: 'MiniInfos-macos-arm64.dmg', label: 'Apple Silicon' },
  { platform: 'macos', file: 'MiniInfos-macos-x64.dmg', label: 'Intel' },
  { platform: 'linux', file: 'MiniInfos-linux-x64.tar.gz', label: 'x64' },
  { platform: 'linux', file: 'MiniInfos-linux-arm64.tar.gz', label: 'ARM64' },
] as const;

export interface PlatformSignals {
  platform?: string;
  mobile?: boolean;
  maxTouchPoints?: number;
}

/** Use the browser's basic OS hint when available; no architecture probing or server tracking. */
export function detectPlatform(
  platform: string,
  userAgent = '',
  signals: PlatformSignals = {},
): Platform | null {
  const legacy = `${platform} ${userAgent}`.toLowerCase();
  const hint = signals.platform?.trim().toLowerCase();
  if (
    signals.mobile ||
    /android|iphone|ipad|ipod|cros|windows phone|iemobile/.test(legacy) ||
    /android|ios|chrome os/.test(hint ?? '')
  )
    return null;
  // Safari on iPad can report the same platform and user agent as a Mac.
  if ((/mac/.test(legacy) || hint === 'macos') && (signals.maxTouchPoints ?? 0) > 1) return null;
  const value = hint || legacy;
  if (/windows|win32|win64|winnt/.test(value)) return 'windows';
  if (/mac/.test(value)) return 'macos';
  return /linux/.test(value) ? 'linux' : null;
}

/** The OS chooses a section; the visitor chooses their CPU architecture there. */
export function downloadTarget(platform: Platform | null): string {
  return platform ? `#download-${platform}` : '#downloads';
}

export function availablePackages(release: unknown): Map<string, string> {
  if (!release || typeof release !== 'object') return new Map();
  const data = release as { draft?: boolean; prerelease?: boolean; assets?: unknown };
  if (data.draft || data.prerelease || !Array.isArray(data.assets)) return new Map();
  const found = new Map<string, string>();
  for (const asset of data.assets) {
    if (!asset || typeof asset !== 'object') continue;
    const { name, browser_download_url: address } = asset;
    if (!packages.some((item) => item.file === name) || typeof address !== 'string') continue;
    try {
      const url = new URL(address);
      if (
        url.protocol === 'https:' &&
        url.hostname === 'github.com' &&
        !url.username &&
        !url.password &&
        !url.port &&
        url.pathname.startsWith('/rafaeldossanto/MiniInfos/releases/download/') &&
        decodeURIComponent(url.pathname.split('/').at(-1)!) === name
      )
        found.set(name, address);
    } catch {
      /* Unavailable metadata leaves an honest release-page fallback. */
    }
  }
  return found;
}
