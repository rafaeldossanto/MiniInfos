import {
  MAX_ARCHIVE,
  PackageError,
  unpack,
  validateFiles,
} from '../src/community/creation/package.ts';
import { GithubError } from './github.ts';
import type { AppCredentials } from './github.ts';
import type { Store } from './storage.ts';
import { RetryLater } from './storage.ts';
import { submitCreation, loadReceipt, refreshReceipt, publicReceipt } from './submissions.ts';

interface Limit {
  limit(options: { key: string }): Promise<{ success: boolean }>;
}
export interface Env extends AppCredentials {
  RECEIPTS: Store;
  SEND_LIMITER: Limit;
  READ_LIMITER: Limit;
  SITE_ORIGIN: string;
  TURNSTILE_SITE_KEY: string;
  TURNSTILE_SECRET: string;
}
const configured = (env: Env) =>
  Boolean(
    env.RECEIPTS &&
    env.SEND_LIMITER &&
    env.READ_LIMITER &&
    env.GITHUB_APP_ID &&
    env.GITHUB_INSTALLATION_ID &&
    env.GITHUB_PRIVATE_KEY &&
    env.TURNSTILE_SECRET &&
    env.TURNSTILE_SITE_KEY &&
    /^https:\/\//.test(env.SITE_ORIGIN),
  );
const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), {
    status,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store',
      'X-Content-Type-Options': 'nosniff',
      'Referrer-Policy': 'no-referrer',
    },
  });
const uuid = /^[a-f0-9]{8}-[a-f0-9]{4}-4[a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}$/;
async function boundedBody(request: Request, maximum: number) {
  if (Number(request.headers.get('Content-Length')) > maximum || !request.body)
    throw new PackageError('size');
  const reader = request.body.getReader(),
    chunks: Uint8Array[] = [];
  let size = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    if ((size += value.length) > maximum) {
      await reader.cancel();
      throw new PackageError('size');
    }
    chunks.push(value);
  }
  const bytes = new Uint8Array(size);
  let offset = 0;
  for (const part of chunks) {
    bytes.set(part, offset);
    offset += part.length;
  }
  return bytes;
}

export async function handle(
  request: Request,
  env: Env,
  send: typeof fetch = fetch,
): Promise<Response> {
  const origin = request.headers.get('Origin');
  const headers = {
    'Access-Control-Allow-Origin': env.SITE_ORIGIN,
    Vary: 'Origin',
    'Access-Control-Expose-Headers': 'Retry-After',
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers':
      'Content-Type, X-Turnstile-Token, X-Submission-Id, X-Rights-Confirmed',
  };
  const respond = (r: Response) => {
    for (const [k, v] of Object.entries(headers)) r.headers.set(k, v);
    return r;
  };
  // This service is called only by our form; no wildcard CORS, cookies or secret-bearing redirects.
  if (origin !== env.SITE_ORIGIN) return json({ code: 'origin' }, 403);
  if (request.method === 'OPTIONS') return respond(new Response(null, { status: 204 }));
  const path = new URL(request.url).pathname.replace(/\/$/, '');
  if (path === '/config' && request.method === 'GET')
    return respond(
      json({ enabled: configured(env), siteKey: configured(env) ? env.TURNSTILE_SITE_KEY : null }),
    );
  if (!configured(env)) return respond(json({ code: 'unavailable' }, 503));
  const ip = request.headers.get('CF-Connecting-IP');
  if (!ip) return respond(json({ code: 'unavailable' }, 503));
  try {
    if (path === '/submissions' && request.method === 'POST') {
      if (!(await env.SEND_LIMITER.limit({ key: ip })).success) throw new RetryLater();
      const id = request.headers.get('X-Submission-Id') ?? '';
      if (
        !uuid.test(id) ||
        request.headers.get('X-Rights-Confirmed') !== 'true' ||
        request.headers.get('Content-Type') !== 'application/zip'
      )
        return respond(json({ code: 'request' }, 400));
      const challenge = request.headers.get('X-Turnstile-Token') ?? '';
      if (!challenge || challenge.length > 2048) return respond(json({ code: 'challenge' }, 400));
      const verified = await send('https://challenges.cloudflare.com/turnstile/v0/siteverify', {
        method: 'POST',
        body: new URLSearchParams({
          secret: env.TURNSTILE_SECRET,
          response: challenge,
          remoteip: ip,
          idempotency_key: id,
        }),
        signal: AbortSignal.timeout(10000),
      });
      const result = (await verified.json()) as {
        success: boolean;
        hostname: string;
        action: string;
      };
      if (
        !verified.ok ||
        !result.success ||
        result.hostname !== new URL(env.SITE_ORIGIN).hostname ||
        result.action !== 'community-submit'
      )
        return respond(json({ code: 'challenge' }, 400));
      const creation = validateFiles(unpack(await boundedBody(request, MAX_ARCHIVE)));
      const submission = await submitCreation(id, creation, env, send);
      return respond(json(publicReceipt(submission.receipt), submission.created ? 201 : 200));
    }
    const match = /^\/submissions\/([a-f0-9-]+)$/.exec(path);
    if (match && request.method === 'GET' && uuid.test(match[1])) {
      if (!(await env.READ_LIMITER.limit({ key: ip })).success) throw new RetryLater();
      const receipt = await loadReceipt(env.RECEIPTS, match[1]);
      if (!receipt) return respond(json({ code: 'missing' }, 404));
      return respond(json(publicReceipt(await refreshReceipt(receipt, env, send))));
    }
    return respond(json({ code: 'missing' }, 404));
  } catch (e) {
    if (e instanceof RetryLater) {
      const response = json({ code: 'cooldown', retryAfter: e.seconds }, 429);
      response.headers.set('Retry-After', String(e.seconds));
      return respond(response);
    }
    if (e instanceof PackageError) return respond(json({ code: 'package', error: e.code }, 422));
    if (e instanceof GithubError && e.status === 409)
      return respond(json({ code: 'duplicate' }, 409));
    // Do not expose keys, tokens, GitHub internals, uploader data or stack traces.
    return respond(json({ code: 'unavailable' }, 503));
  }
}
export default { fetch: (request: Request, env: Env) => handle(request, env) };
