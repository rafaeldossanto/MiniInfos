export type Receipt = {
  id: string;
  packageId: string;
  version: string;
  createdAt: string;
  retryAfter?: number;
  state:
    'validating' | 'review' | 'changes' | 'publishing' | 'published' | 'rejected' | 'unavailable';
};
export type ServiceConfig = { enabled: boolean; siteKey: string | null };
export class SubmissionError extends Error {
  readonly code: string;
  readonly retryAfter: number;
  constructor(code: string, retryAfter = 0) {
    super(code);
    this.code = code;
    this.retryAfter = Number.isFinite(retryAfter) ? Math.max(0, Math.ceil(retryAfter)) : 60;
  }
}
export function serviceBase(value: string | undefined): string {
  if (!value) return '';
  try {
    const url = new URL(value);
    if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash)
      return '';
    return url.href.replace(/\/$/, '');
  } catch {
    return '';
  }
}
export const apiBase = serviceBase(import.meta.env?.VITE_COMMUNITY_API);
export async function service<T>(path: string, options: RequestInit = {}): Promise<T> {
  if (!apiBase) throw new SubmissionError('unavailable');
  try {
    const response = await fetch(apiBase + path, {
      ...options,
      credentials: 'omit',
      referrerPolicy: 'no-referrer',
      signal: AbortSignal.timeout(90000),
    });
    const body = await response.json();
    if (!response.ok)
      throw new SubmissionError(
        body.code === 'package' ? body.error : (body.code ?? 'unavailable'),
        Number(response.headers.get('Retry-After') ?? body.retryAfter ?? 0),
      );
    return body as T;
  } catch (e) {
    if (e instanceof SubmissionError) throw e;
    throw new SubmissionError('network');
  }
}
