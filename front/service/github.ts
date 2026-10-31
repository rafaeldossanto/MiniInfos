import { project } from '../src/config.ts';
import type { Creation } from '../src/community/creation/package.ts';

export interface AppCredentials {
  GITHUB_APP_ID: string;
  GITHUB_INSTALLATION_ID: string;
  GITHUB_PRIVATE_KEY: string;
}
export class GithubError extends Error {
  readonly status: number;
  constructor(status: number) {
    super('GitHub request failed');
    this.status = status;
  }
}
export const repoPath = '/repos' + new URL(project.repository).pathname;
const bytes64 = (bytes: Uint8Array) => {
  let value = '';
  for (let i = 0; i < bytes.length; i += 8192)
    value += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(value);
};
const url64 = (bytes: Uint8Array) =>
  bytes64(bytes).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');

const tokens = new WeakMap<
  AppCredentials,
  { identity: string; pending?: Promise<string>; token?: string; expires: number }
>();
/** Tokens never leave server memory. Concurrent calls share one exchange. */
export async function installationToken(env: AppCredentials, send: typeof fetch): Promise<string> {
  const identity =
    env.GITHUB_APP_ID + ':' + env.GITHUB_INSTALLATION_ID + ':' + env.GITHUB_PRIVATE_KEY;
  let cached = tokens.get(env);
  if (!cached || cached.identity !== identity) {
    cached = { identity, expires: 0 };
    tokens.set(env, cached);
  }
  if (cached.token && Date.now() < cached.expires) return cached.token;
  if (cached.pending) return cached.pending;
  const entry = cached;
  entry.pending = issueToken(env, send)
    .then((issued) => {
      entry.token = issued.token;
      entry.expires = Date.parse(issued.expires_at) - 60000;
      return issued.token;
    })
    .finally(() => {
      entry.pending = undefined;
    });
  return entry.pending;
}
export function invalidateToken(env: AppCredentials) {
  tokens.delete(env);
}

async function issueToken(
  env: AppCredentials,
  send: typeof fetch,
): Promise<{ token: string; expires_at: string }> {
  const now = Math.floor(Date.now() / 1000);
  const encode = (v: unknown) => url64(new TextEncoder().encode(JSON.stringify(v)));
  const unsigned =
    encode({ alg: 'RS256', typ: 'JWT' }) +
    '.' +
    encode({ iat: now - 60, exp: now + 540, iss: env.GITHUB_APP_ID });
  const pem = env.GITHUB_PRIVATE_KEY.replace(/\\n/g, '\n').replace(/-----[^-]+-----|\s/g, '');
  let der = Uint8Array.from(atob(pem), (c) => c.charCodeAt(0));
  if (env.GITHUB_PRIVATE_KEY.includes('BEGIN RSA PRIVATE KEY')) {
    // GitHub downloads PKCS#1 keys; WebCrypto accepts PKCS#8. Wrap the DER, without exporting or logging it.
    const block = (tag: number, data: Uint8Array) => {
      let n = data.length;
      const size: number[] = [];
      while (n) {
        size.unshift(n & 255);
        n >>>= 8;
      }
      return new Uint8Array([
        tag,
        ...(data.length < 128 ? [data.length] : [128 + size.length, ...size]),
        ...data,
      ]);
    };
    const algorithm = [48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 1, 5, 0];
    der = block(48, new Uint8Array([2, 1, 0, ...algorithm, ...block(4, der)]));
  }
  const key = await crypto.subtle.importKey(
    'pkcs8',
    new Uint8Array(der),
    { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' },
    false,
    ['sign'],
  );
  const signed = await crypto.subtle.sign(
    'RSASSA-PKCS1-v1_5',
    key,
    new TextEncoder().encode(unsigned),
  );
  const response = await send(
    `https://api.github.com/app/installations/${env.GITHUB_INSTALLATION_ID}/access_tokens`,
    {
      method: 'POST',
      headers: headers(unsigned + '.' + url64(new Uint8Array(signed))),
      body: JSON.stringify({
        repository_ids: [project.repositoryId],
        permissions: { contents: 'write', pull_requests: 'write', checks: 'read', actions: 'read' },
      }),
      signal: AbortSignal.timeout(15000),
    },
  );
  if (!response.ok) throw new GithubError(response.status);
  return (await response.json()) as { token: string; expires_at: string };
}
function headers(token: string) {
  return {
    Authorization: 'Bearer ' + token,
    'User-Agent': 'MiniInfos-community',
    Accept: 'application/vnd.github+json',
    'X-GitHub-Api-Version': '2026-03-10',
    'Content-Type': 'application/json',
  };
}
export function github(token: string, send: typeof fetch) {
  return async <T>(path: string, method = 'GET', body?: unknown): Promise<T> => {
    const response = await send('https://api.github.com' + path, {
      method,
      headers: headers(token),
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: AbortSignal.timeout(15000),
    });
    if (!response.ok) throw new GithubError(response.status);
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
  };
}
export type Github = ReturnType<typeof github>;

/** A fixed prefix and fixed 100644 modes keep submitted files out of app code and workflow paths. Never merge here. */
export async function openSubmission(
  api: Github,
  creation: Creation,
  resume?: { id: string; digest: string },
): Promise<{ number: number; sha: string }> {
  const m = creation.manifest;
  const prefix = 'marketplace/packages/' + m.id + '/';
  try {
    await api(`${repoPath}/contents/${prefix}?ref=main`);
    throw new GithubError(409);
  } catch (e) {
    if (!(e instanceof GithubError) || e.status !== 404) throw e;
  }
  const head = await api<{ object: { sha: string } }>(`${repoPath}/git/ref/heads/main`);
  const parent = await api<{ tree: { sha: string } }>(`${repoPath}/git/commits/${head.object.sha}`);
  const branch = 'community-submit/' + m.id;
  const marker = resume ? `<!-- miniinfos:${resume.id}:${resume.digest} -->` : '';
  let branchSha = '';
  try {
    const tree: { path: string; mode: string; type: string; sha: string }[] = [];
    for (const [path, data] of Object.entries(creation.files)) {
      const blob = await api<{ sha: string }>(`${repoPath}/git/blobs`, 'POST', {
        content: bytes64(data),
        encoding: 'base64',
      });
      tree.push({ path: prefix + path, mode: '100644', type: 'blob', sha: blob.sha });
    }
    const next = await api<{ sha: string }>(`${repoPath}/git/trees`, 'POST', {
      base_tree: parent.tree.sha,
      tree,
    });
    const commit = await api<{ sha: string }>(`${repoPath}/git/commits`, 'POST', {
      message: `Add community package ${m.id} ${m.version}\n${marker}`,
      tree: next.sha,
      parents: [head.object.sha],
    });
    branchSha = commit.sha;
    try {
      // Publish the ref only after the complete, identifiable commit exists.
      await api(`${repoPath}/git/refs`, 'POST', { ref: 'refs/heads/' + branch, sha: commit.sha });
    } catch (e) {
      if (!(e instanceof GithubError) || e.status !== 422 || !resume) throw e;
      const existing = await api<{ object: { sha: string } }>(
        `${repoPath}/git/ref/heads/${branch}`,
      );
      const saved = await api<{ message: string }>(
        `${repoPath}/git/commits/${existing.object.sha}`,
      );
      if (!saved.message.endsWith('\n' + marker)) throw new GithubError(409);
      branchSha = existing.object.sha;
      const pulls = await api<{ number: number; body: string; head: { sha: string } }[]>(
        `${repoPath}/pulls?state=all&head=rafaeldossanto:${branch}`,
      );
      const own = pulls.find((p) => p.body?.includes(marker) && p.head.sha === branchSha);
      if (own) return { number: own.number, sha: own.head.sha };
    }
    // A JSON code block escapes all user text; author/license declarations remain explicit for review.
    const metadata = JSON.stringify(
      { id: m.id, name: m.name, author: m.author, version: m.version, license: m.license },
      null,
      2,
    )
      .replace(/`/g, '\\u0060')
      .replace(/</g, '\\u003c');
    const pull = await api<{ number: number }>(`${repoPath}/pulls`, 'POST', {
      title: `Community: ${m.id} ${m.version}`,
      head: branch,
      base: 'main',
      body:
        (marker ? marker + '\n' : '') +
        'Submitted through the MiniInfos Community page. The uploader declared permission to redistribute these files.\n\n```json\n' +
        metadata +
        '\n```\n\nThe catalog validator must pass and a maintainer must review before merging. No executable code is accepted.',
    });
    return { number: pull.number, sha: branchSha };
  } catch (e) {
    // A lost POST response may already have created the PR. Recover only this exact submission.
    if (branchSha && marker) {
      try {
        const pulls = await api<{ number: number; body: string; head: { sha: string } }[]>(
          `${repoPath}/pulls?state=all&head=rafaeldossanto:${branch}`,
        );
        const own = pulls.find((p) => p.body?.includes(marker) && p.head.sha === branchSha);
        if (own) return { number: own.number, sha: own.head.sha };
      } catch {
        /* Retain the identifiable branch so a later retry can recover safely. */
      }
    }
    if (e instanceof GithubError && e.status === 422) throw new GithubError(409);
    throw e;
  }
}

export type SubmissionState =
  'validating' | 'review' | 'changes' | 'publishing' | 'published' | 'rejected' | 'unavailable';
export async function reviewState(api: Github, number: number): Promise<SubmissionState> {
  const pull = await api<{ merged: boolean; state: string; head: { sha: string } }>(
    `${repoPath}/pulls/${number}`,
  );
  if (pull.merged) return 'publishing';
  if (pull.state === 'closed') return 'rejected';
  const checks = await api<{
    check_runs: {
      name: string;
      status: string;
      conclusion: string | null;
      app?: { slug: string };
    }[];
  }>(`${repoPath}/commits/${pull.head.sha}/check-runs?filter=latest&per_page=100`);
  // Never report approval based on an unrelated check or on a skipped/cancelled validator.
  const catalog = checks.check_runs.find(
    (c) => c.name === 'catalog' && c.app?.slug === 'github-actions',
  );
  if (!catalog || catalog.status !== 'completed') return 'validating';
  return catalog.conclusion === 'success' ? 'review' : 'changes';
}
