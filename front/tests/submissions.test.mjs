import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { zipSync } from 'fflate';
import { handle } from '../service/worker.ts';
import { reviewState, GithubError, openSubmission, installationToken } from '../service/github.ts';
import { createPrivateKey, createHash } from 'node:crypto';
import { validateFiles } from '../src/community/creation/package.ts';
import { fingerprint } from '../service/submissions.ts';
import { budgetedFetch } from '../service/github-budget.ts';
import { RetryLater, write, withLease } from '../service/storage.ts';
const origin = 'https://miniinfos.com',
  id = 'f7358318-3d50-4d00-a1c2-56f87715b35b';
const source = {
  'manifest.json': new Uint8Array(
    readFileSync(
      new URL('../../marketplace/packages/community.ocean/manifest.json', import.meta.url),
    ),
  ),
};
const archive = zipSync(source);
function expireStatus(stored) {
  for (const [key, value] of stored) {
    if (!key.startsWith('receipts/')) continue;
    const receipt = JSON.parse(value);
    receipt.nextCheckAt = 0;
    receipt.retryAt = 0;
    stored.set(key, JSON.stringify(receipt));
  }
}
const withId = (nextId, body = archive) => {
  const headers = new Headers(request().headers);
  headers.set('X-Submission-Id', nextId);
  return request('/submissions', { headers, body });
};

const keys = await crypto.subtle.generateKey(
  {
    name: 'RSASSA-PKCS1-v1_5',
    modulusLength: 2048,
    publicExponent: new Uint8Array([1, 0, 1]),
    hash: 'SHA-256',
  },
  true,
  ['sign', 'verify'],
);
const pem =
  '-----BEGIN PRIVATE KEY-----\n' +
  Buffer.from(await crypto.subtle.exportKey('pkcs8', keys.privateKey)).toString('base64') +
  '\n-----END PRIVATE KEY-----';
const json = (value, status = 200) =>
  new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });

test('the GitHub-downloaded PKCS1 private key works without manual conversion', async () => {
  const pkcs1 = createPrivateKey(pem).export({ format: 'pem', type: 'pkcs1' }).toString();
  const s = setup({ GITHUB_PRIVATE_KEY: pkcs1 });
  assert.equal(await installationToken(s.env, s.send), 'installation-token');
});
function setup(overrides = {}) {
  const stored = new Map(),
    calls = [];
  const env = {
    SITE_ORIGIN: origin,
    TURNSTILE_SITE_KEY: 'public-key',
    TURNSTILE_SECRET: 'test-secret',
    GITHUB_APP_ID: '123',
    GITHUB_INSTALLATION_ID: '456',
    GITHUB_PRIVATE_KEY: pem,
    SEND_LIMITER: { limit: async () => ({ success: true }) },
    READ_LIMITER: { limit: async () => ({ success: true }) },
    RECEIPTS: {
      get: async (k) =>
        stored.has(k)
          ? {
              etag: createHash('sha256').update(stored.get(k)).digest('hex'),
              json: async () => JSON.parse(stored.get(k)),
            }
          : null,
      put: async (k, v, options) => {
        const existing = stored.get(k);
        const etag =
          existing === undefined ? undefined : createHash('sha256').update(existing).digest('hex');
        if (options?.onlyIf.get('If-None-Match') === '*' && existing !== undefined) return null;
        if (options?.onlyIf.has('If-Match') && options.onlyIf.get('If-Match') !== etag) return null;
        stored.set(k, v);
        return { etag: createHash('sha256').update(v).digest('hex') };
      },
    },
    ...overrides,
  };
  const send = async (url, init = {}) => {
    calls.push({ url, init });
    const body = init.body ? JSON.parse(typeof init.body === 'string' ? init.body : '{}') : {};
    if (url.includes('/siteverify'))
      return json({ success: true, hostname: 'miniinfos.com', action: 'community-submit' });
    if (url.includes('/access_tokens'))
      return json({
        token: 'installation-token',
        expires_at: new Date(Date.now() + 3600000).toISOString(),
      });
    if (url.includes('/contents/')) return json({ message: 'Not Found' }, 404);
    if (url.endsWith('/git/ref/heads/main')) return json({ object: { sha: 'main-sha' } });
    if (url.endsWith('/git/commits/main-sha')) return json({ tree: { sha: 'base-tree' } });
    if (url.endsWith('/git/blobs')) return json({ sha: 'blob-sha' });
    if (url.endsWith('/git/trees')) return json({ sha: 'tree-sha' });
    if (url.endsWith('/git/commits')) return json({ sha: 'new-sha' });
    if (url.endsWith('/pulls') && init.method === 'POST') return json({ number: 17 });
    if (url.endsWith('/pulls/17'))
      return json({ state: 'open', merged: false, head: { sha: 'new-sha' } });
    if (url.includes('/check-runs'))
      return json({
        check_runs: [
          {
            name: 'catalog',
            status: 'completed',
            conclusion: 'success',
            app: { slug: 'github-actions' },
          },
        ],
      });
    if (url.includes('/git/refs')) return json({ ref: body.ref });
    throw new Error('Unexpected request ' + url);
  };
  return { env, stored, calls, send };
}
function request(path = '/submissions', options = {}) {
  return new Request('https://api.example.com' + path, {
    method: 'POST',
    headers: {
      Origin: origin,
      'CF-Connecting-IP': '192.0.2.1',
      'Content-Type': 'application/zip',
      'X-Submission-Id': id,
      'X-Turnstile-Token': 'test-challenge',
      'X-Rights-Confirmed': 'true',
    },
    body: archive,
    ...options,
  });
}

test('submission creates one scoped PR and receipt; author never needs a GitHub account', async () => {
  const { env, stored, calls, send } = setup();
  const r = await handle(request(), env, send);
  assert.equal(r.status, 201);
  assert.equal((await r.json()).state, 'validating');
  const exchange = calls.find((c) => c.url.includes('/access_tokens'));
  assert.deepEqual(JSON.parse(exchange.init.body).repository_ids, [1432597159]);
  const jwt = exchange.init.headers.Authorization.slice(7).split('.');
  assert.ok(
    await crypto.subtle.verify(
      'RSASSA-PKCS1-v1_5',
      keys.publicKey,
      Buffer.from(jwt[2], 'base64url'),
      new TextEncoder().encode(jwt.slice(0, 2).join('.')),
    ),
  );
  const tree = JSON.parse(calls.find((c) => c.url.endsWith('/git/trees')).init.body);
  assert.equal(tree.base_tree, 'base-tree');
  assert.ok(
    tree.tree.every(
      (f) => f.path.startsWith('marketplace/packages/community.ocean/') && f.mode === '100644',
    ),
  );
  assert.ok(!calls.some((c) => c.url.endsWith('/merge')));
  assert.ok(
    ![...stored.values()].some(
      (v) => v.includes('test-secret') || v.includes('192.0.2.1') || v.includes('PRIVATE KEY'),
    ),
  );
  const repeated = await handle(request(), env, send);
  assert.equal(repeated.status, 200);
  assert.equal(calls.filter((c) => c.url.endsWith('/pulls') && c.init.method === 'POST').length, 1);
  expireStatus(stored);
  const status = await handle(
    request('/submissions/' + id, { method: 'GET', body: undefined }),
    env,
    send,
  );
  assert.equal((await status.json()).state, 'review');
});
test('unconfigured service, unapproved origins, failed verification and rate limits fail closed', async () => {
  for (const [override, req, expected] of [
    [{ GITHUB_PRIVATE_KEY: '' }, request(), 503],
    [{}, request('/submissions', { headers: { Origin: 'https://evil.example' } }), 403],
    [{ SEND_LIMITER: { limit: async () => ({ success: false }) } }, request(), 429],
  ]) {
    const s = setup(override);
    const r = await handle(req, s.env, s.send);
    assert.equal(r.status, expected);
    assert.equal(s.calls.length, 0);
  }
  const s = setup();
  const invalid = await handle(request(), s.env, async () =>
    json({ success: true, hostname: 'evil.example', action: 'community-submit' }),
  );
  assert.equal(invalid.status, 400);
  assert.equal(s.stored.size, 0);
});
test('invalid files never reach GitHub, and errors do not disclose tokens or upstream responses', async () => {
  const s = setup();
  const r = await handle(
    request('/submissions', { body: zipSync({ '../manifest.json': source['manifest.json'] }) }),
    s.env,
    s.send,
  );
  assert.equal(r.status, 422);
  assert.equal((await r.json()).error, 'path');
  assert.equal(s.calls.length, 1);
  const failure = setup();
  const unavailable = await handle(request(), failure.env, async (url, init) =>
    url.includes('/siteverify')
      ? failure.send(url, init)
      : new Response('secret-backend-body', { status: 500 }),
  );
  assert.equal(unavailable.status, 429);
  assert.deepEqual(await unavailable.json(), { code: 'cooldown', retryAfter: 60 });
});
test('existing package identities cannot be replaced', async () => {
  let writes = 0;
  const api = async (path, method = 'GET') => {
    if (method !== 'GET') writes++;
    return {};
  };
  await assert.rejects(
    () => openSubmission(api, validateFiles(source)),
    (e) => e instanceof GithubError && e.status === 409,
  );
  assert.equal(writes, 0);
});
test('review status uses the exact catalog check; skipped, pending and unrelated checks never mean approved', async () => {
  const state = async (checks, pull = { state: 'open', merged: false, head: { sha: 'head' } }) =>
    reviewState(async (path) => (path.includes('/check-runs') ? { check_runs: checks } : pull), 17);
  assert.equal(await state([]), 'validating');
  assert.equal(
    await state([
      {
        name: 'front',
        status: 'completed',
        conclusion: 'success',
        app: { slug: 'github-actions' },
      },
    ]),
    'validating',
  );
  for (const conclusion of ['failure', 'cancelled', 'skipped', 'timed_out'])
    assert.equal(
      await state([
        { name: 'catalog', status: 'completed', conclusion, app: { slug: 'github-actions' } },
      ]),
      'changes',
    );
  assert.equal(
    await state([
      {
        name: 'catalog',
        status: 'completed',
        conclusion: 'success',
        app: { slug: 'github-actions' },
      },
    ]),
    'review',
  );
  assert.equal(await state([], { state: 'closed', merged: true }), 'publishing');
  assert.equal(await state([], { state: 'closed', merged: false }), 'rejected');
});
test('merged submissions become public only after the website snapshot contains that exact version', async () => {
  const s = setup();
  await s.env.RECEIPTS.put(
    'receipts/' + id + '.json',
    JSON.stringify({
      id,
      packageId: 'community.ocean',
      version: '1.0.0',
      state: 'review',
      createdAt: new Date().toISOString(),
      number: 17,
    }),
  );
  let published = false;
  const send = async (url, init) =>
    url.endsWith('/pulls/17')
      ? json({ merged: true, state: 'closed' })
      : url.endsWith('/community/catalog.json')
        ? json({ packages: published ? [{ id: 'community.ocean', version: '1.0.0' }] : [] })
        : s.send(url, init);
  assert.equal(
    (
      await (
        await handle(request('/submissions/' + id, { method: 'GET', body: undefined }), s.env, send)
      ).json()
    ).state,
    'publishing',
  );
  published = true;
  expireStatus(s.stored);
  assert.equal(
    (
      await (
        await handle(request('/submissions/' + id, { method: 'GET', body: undefined }), s.env, send)
      ).json()
    ).state,
    'published',
  );
});

test('same content uses one receipt with another UUID, ZIP timestamp and compression', async () => {
  const s = setup();
  assert.equal((await handle(request(), s.env, s.send)).status, 201);
  const other = 'f7358318-3d50-4d00-a1c2-56f87715b35c';
  const repeat = await handle(
    withId(other, zipSync(source, { level: 0, mtime: new Date(2020, 0, 1) })),
    s.env,
    s.send,
  );
  assert.equal(repeat.status, 200);
  assert.equal((await repeat.json()).id, id);
  assert.equal(
    s.calls.filter((c) => c.url.endsWith('/pulls') && c.init.method === 'POST').length,
    1,
  );
  const alias = await handle(
    request('/submissions/' + other, { method: 'GET', body: undefined }),
    s.env,
    s.send,
  );
  assert.equal((await alias.json()).id, id);
  const changed = JSON.parse(new TextDecoder().decode(source['manifest.json']));
  changed.description = 'Changed content';
  const changedFiles = { 'manifest.json': new TextEncoder().encode(JSON.stringify(changed)) };
  assert.notEqual(
    await fingerprint(validateFiles(source)),
    await fingerprint(validateFiles(changedFiles)),
  );
  assert.equal((await handle(withId(id, zipSync(changedFiles)), s.env, s.send)).status, 409);
});

test('concurrent duplicates are serialized across separate Worker environments', async () => {
  const s = setup();
  let release, started;
  const gate = new Promise((r) => (release = r)),
    entered = new Promise((r) => (started = r));
  const delayed = async (url, init) => {
    if (url.includes('/access_tokens')) {
      started();
      await gate;
    }
    return s.send(url, init);
  };
  const first = handle(request(), s.env, delayed);
  await entered;
  const second = await handle(withId('f7358318-3d50-4d00-a1c2-56f87715b35c'), { ...s.env }, s.send);
  release();
  assert.equal(second.status, 429);
  assert.ok(Number(second.headers.get('Retry-After')) > 0);
  assert.equal((await first).status, 201);
  assert.equal(
    s.calls.filter((c) => c.url.endsWith('/pulls') && c.init.method === 'POST').length,
    1,
  );
});

test('temporary failure allows retry after cooldown instead of returning false success', async () => {
  const s = setup();
  let fail = true;
  const flaky = async (url, init) =>
    url.includes('/contents/') && fail ? json({}, 503) : s.send(url, init);
  const first = await handle(request(), s.env, flaky);
  assert.equal(first.status, 429);
  assert.equal(first.headers.get('Retry-After'), '60');
  const count = s.calls.length;
  fail = false;
  assert.equal((await handle(request(), s.env, flaky)).status, 429);
  assert.equal(s.calls.length, count + 1); // Only Turnstile, no GitHub requests.
  expireStatus(s.stored);
  const retry = await handle(request(), s.env, flaky);
  assert.equal(retry.status, 201);
  assert.equal((await retry.json()).id, id);
});

test('status cache and token reuse bound concurrent and repeated reads', async () => {
  const s = setup();
  await handle(request(), s.env, s.send);
  expireStatus(s.stored);
  const before = s.calls.length;
  await Promise.all(
    Array.from({ length: 4 }, () =>
      handle(
        request('/submissions/' + id, { method: 'GET', body: undefined }),
        { ...s.env },
        s.send,
      ),
    ),
  );
  assert.equal(s.calls.slice(before).filter((c) => c.url.includes('/check-runs')).length, 1);
  const cachedCount = s.calls.length;
  await handle(request('/submissions/' + id, { method: 'GET', body: undefined }), s.env, s.send);
  assert.equal(s.calls.length, cachedCount);
  const t = setup();
  await Promise.all([installationToken(t.env, t.send), installationToken(t.env, t.send)]);
  await installationToken(t.env, t.send);
  assert.equal(t.calls.filter((c) => c.url.includes('/access_tokens')).length, 1);
});

test('GitHub Retry-After and shared budget protect the allowance across visitors', async () => {
  const s = setup();
  let calls = 0;
  const upstream = async () => {
    calls++;
    return new Response('', { status: 429, headers: { 'Retry-After': '120' } });
  };
  await assert.rejects(
    () => budgetedFetch(s.env.RECEIPTS, upstream)('https://api.github.com/test'),
    (e) => e instanceof RetryLater && e.seconds >= 119,
  );
  await assert.rejects(
    () => budgetedFetch(s.env.RECEIPTS, upstream)('https://api.github.com/test'),
    RetryLater,
  );
  assert.equal(calls, 1);
  s.stored.delete('github/pause');
  s.stored.set('github/budget', JSON.stringify({ start: Date.now(), count: 3000 }));
  await assert.rejects(
    () => budgetedFetch(s.env.RECEIPTS, upstream)('https://api.github.com/test'),
    RetryLater,
  );
  assert.equal(calls, 1);
});

test('a lost PR response can be recovered after restart without creating another PR', async () => {
  const s = setup();
  let phase = 'lost',
    branchSha = '',
    message = '',
    body = '',
    posts = 0;
  const send = async (url, init = {}) => {
    const data = typeof init.body === 'string' ? JSON.parse(init.body) : {};
    if (url.endsWith('/git/commits') && init.method === 'POST') message = data.message;
    if (url.endsWith('/git/refs') && init.method === 'POST') {
      if (branchSha) return json({}, 422);
      branchSha = data.sha;
      return json({ ref: data.ref });
    }
    if (url.endsWith('/git/ref/heads/community-submit/community.ocean'))
      return json({ object: { sha: branchSha } });
    if (url.endsWith('/git/commits/new-sha')) return json({ message });
    if (url.endsWith('/pulls') && init.method === 'POST') {
      posts++;
      body = data.body;
      throw new Error('Connection lost after PR creation');
    }
    if (url.includes('/pulls?')) {
      if (phase === 'lost') throw new Error('Connection still down');
      return json([{ number: 17, body, head: { sha: branchSha } }]);
    }
    return s.send(url, init);
  };
  assert.equal((await handle(request(), s.env, send)).status, 429);
  assert.ok(!body.includes(id), 'tracking UUID stays private');
  phase = 'recovered';
  expireStatus(s.stored);
  assert.equal((await handle(request(), { ...s.env }, send)).status, 201);
  assert.equal(posts, 1);
});

test('a conflicting branch is not adopted or overwritten by a retry', async () => {
  const s = setup();
  let writesToExisting = 0;
  const send = async (url, init = {}) => {
    if (url.endsWith('/git/refs') && init.method === 'POST') return json({}, 422);
    if (url.includes('/git/ref/heads/community-submit/'))
      return json({ object: { sha: 'someone-else' } });
    if (url.endsWith('/git/commits/someone-else')) return json({ message: 'Unrelated change' });
    if (init.method === 'PATCH' || init.method === 'DELETE' || url.endsWith('/pulls'))
      writesToExisting++;
    if (url.includes('/pulls?')) return json([]);
    return s.send(url, init);
  };
  assert.equal((await handle(request(), s.env, send)).status, 409);
  assert.equal(writesToExisting, 0);
});

test('an interrupted upload becomes retryable instead of polling validation forever', async () => {
  const s = setup();
  await s.env.RECEIPTS.put(
    'receipts/' + id + '.json',
    JSON.stringify({
      id,
      packageId: 'community.ocean',
      version: '1.0.0',
      state: 'validating',
      createdAt: new Date().toISOString(),
      retryAt: Date.now() - 1000,
    }),
  );
  const response = await handle(
    request('/submissions/' + id, { method: 'GET', body: undefined }),
    s.env,
    s.send,
  );
  assert.equal(response.status, 200);
  assert.equal((await response.json()).state, 'unavailable');
  assert.equal(s.calls.length, 0);
});

test('R2 throttling retries the same condition and a failed lease release keeps the saved result', async () => {
  const s = setup();
  let attempts = 0;
  const conditions = new Headers({ 'If-None-Match': '*' });
  const store = {
    ...s.env.RECEIPTS,
    put: async (key, value, options) => {
      attempts++;
      assert.equal(options.onlyIf, conditions);
      if (attempts === 1) throw new Error('put: Please reduce your request rate. (10058)');
      return s.env.RECEIPTS.put(key, value, options);
    },
  };
  assert.ok(await write(store, 'example', '{}', { onlyIf: conditions }));
  assert.equal(attempts, 2);
  const releaseFails = {
    ...s.env.RECEIPTS,
    put: async (key, value, options) => {
      if (JSON.parse(value).until === 0) throw new Error('Storage unavailable');
      return s.env.RECEIPTS.put(key, value, options);
    },
  };
  assert.equal(
    await withLease(releaseFails, 'saved', async () => 'receipt saved'),
    'receipt saved',
  );
  await assert.rejects(() => withLease(releaseFails, 'saved', async () => 'must wait'), RetryLater);
});
