export type Platform = 'windows' | 'macos' | 'linux';
export const releaseApi = 'https://api.github.com/repositories/1432597159/releases/latest';
export const releasePage = 'https://github.com/rafaeldossanto/MiniInfos/releases/latest';
export const packages = [
  { platform: 'windows', file: 'MiniInfos-Setup.exe', label: 'Windows · x64' },
  { platform: 'macos', file: 'MiniInfos-macos-arm64.dmg', label: 'Apple Silicon' },
  { platform: 'macos', file: 'MiniInfos-macos-x64.dmg', label: 'Intel' },
  { platform: 'linux', file: 'MiniInfos-linux-x64.tar.gz', label: 'x64' },
  { platform: 'linux', file: 'MiniInfos-linux-arm64.tar.gz', label: 'ARM64' },
] as const;

export function detectPlatform(platform: string, userAgent = ''): Platform | null {
  const value = `${platform} ${userAgent}`.toLowerCase();
  if (/android|iphone|ipad/.test(value)) return null;
  if (/win/.test(value)) return 'windows';
  if (/mac/.test(value)) return 'macos';
  return /linux|x11/.test(value) ? 'linux' : null;
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
      if (url.protocol === 'https:' && url.hostname === 'github.com' && !url.username && !url.password && !url.port &&
        url.pathname.startsWith('/rafaeldossanto/MiniInfos/releases/download/') && decodeURIComponent(url.pathname.split('/').at(-1)!) === name) found.set(name, address);
    } catch { /* Unavailable metadata leaves an honest release-page fallback. */ }
  }
  return found;
}
