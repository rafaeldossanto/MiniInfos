import type { Creation } from '../src/community/creation/package.ts';
import {
  github,
  GithubError,
  installationToken,
  invalidateToken,
  openSubmission,
  reviewState,
} from './github.ts';
import type { AppCredentials, SubmissionState } from './github.ts';
import { budgetedFetch } from './github-budget.ts';
import { read, write, update, withLease, remaining, RetryLater } from './storage.ts';
import type { Store } from './storage.ts';

export interface Receipt {
  id: string;
  packageId: string;
  version: string;
  state: SubmissionState;
  createdAt: string;
  number?: number;
  sha?: string;
  digest?: string;
  nextCheckAt?: number;
  retryAt?: number;
}
export interface SubmissionEnv extends AppCredentials {
  RECEIPTS: Store;
  SITE_ORIGIN: string;
}
export const receiptKey = (id: string) => 'receipts/' + id + '.json';
export const publicReceipt = (r: Receipt) => {
  // An interrupted request without a PR needs the original files resubmitted, not endless polling.
  const state =
    !r.number && r.state === 'validating' && !remaining(r.retryAt) ? 'unavailable' : r.state;
  return {
    id: r.id,
    packageId: r.packageId,
    version: r.version,
    state,
    createdAt: r.createdAt,
    retryAfter: remaining(!r.number ? r.retryAt : r.nextCheckAt),
  };
};
const hash = async (bytes: Uint8Array) =>
  Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new Uint8Array(bytes))), (b) =>
    b.toString(16).padStart(2, '0'),
  ).join('');
/** Stable across ZIP compression, timestamps and file order; no file contents are persisted. */
export async function fingerprint(creation: Creation) {
  const entries: [string, string][] = [];
  for (const name of Object.keys(creation.files).sort())
    entries.push([name, await hash(creation.files[name])]);
  return hash(new TextEncoder().encode(JSON.stringify(entries)));
}
export async function loadReceipt(store: Store, id: string): Promise<Receipt | null> {
  const receipt = await read<Receipt | { alias: string }>(store, receiptKey(id));
  return receipt && 'alias' in receipt ? read<Receipt>(store, receiptKey(receipt.alias)) : receipt;
}

export async function submitCreation(
  id: string,
  creation: Creation,
  env: SubmissionEnv,
  send: typeof fetch,
) {
  const store = env.RECEIPTS,
    digest = await fingerprint(creation);
  return withLease(store, 'request/' + id, async () => {
    // A request ID cannot silently be reused for different content.
    await update<{ digest: string }>(store, 'requests/' + id, (old) => {
      if (old && old.digest !== digest) throw new GithubError(409);
      return { digest };
    });
    return withLease(store, 'package/' + creation.manifest.id, async () => {
      const previous = await loadReceipt(store, id);
      if (previous && previous.digest !== digest) throw new GithubError(409);
      const indexed = await read<{ id: string }>(store, 'fingerprints/' + digest);
      let receipt = indexed ? await loadReceipt(store, indexed.id) : previous;
      if (receipt && receipt.id !== id)
        await write(store, receiptKey(id), JSON.stringify({ alias: receipt.id }));
      if (receipt?.number) return { receipt, created: false };
      if (receipt && remaining(receipt.retryAt)) throw new RetryLater(remaining(receipt.retryAt));
      receipt ??= {
        id,
        digest,
        packageId: creation.manifest.id,
        version: creation.manifest.version,
        state: 'validating',
        createdAt: new Date().toISOString(),
      };
      // The retry deadline also survives a terminated Worker/request.
      receipt.retryAt = Date.now() + 120000;
      receipt.state = 'validating';
      await write(store, receiptKey(receipt.id), JSON.stringify(receipt));
      await write(store, 'fingerprints/' + digest, JSON.stringify({ id: receipt.id }));
      try {
        const limited = budgetedFetch(store, send, Object.keys(creation.files).length + 14);
        const token = await installationToken(env, limited);
        const proof = await hash(new TextEncoder().encode(receipt.id));
        const pull = await openSubmission(github(token, limited), creation, { id: proof, digest });
        receipt.number = pull.number;
        receipt.sha = pull.sha;
        receipt.retryAt = 0;
        receipt.nextCheckAt = Date.now() + 60000;
        await write(store, receiptKey(receipt.id), JSON.stringify(receipt));
        return { receipt, created: true };
      } catch (error) {
        if (error instanceof GithubError && error.status === 401) invalidateToken(env);
        receipt.state = 'unavailable';
        receipt.retryAt = Date.now() + (error instanceof RetryLater ? error.seconds : 60) * 1000;
        await write(store, receiptKey(receipt.id), JSON.stringify(receipt));
        if (error instanceof GithubError && error.status === 409) throw error;
        throw new RetryLater(remaining(receipt.retryAt));
      }
    });
  });
}

export async function refreshReceipt(initial: Receipt, env: SubmissionEnv, send: typeof fetch) {
  if (
    !initial.number ||
    ['published', 'rejected'].includes(initial.state) ||
    remaining(initial.nextCheckAt)
  )
    return initial;
  try {
    return await withLease(env.RECEIPTS, 'package/' + initial.packageId, async () => {
      const receipt = (await loadReceipt(env.RECEIPTS, initial.id))!;
      if (
        !receipt.number ||
        ['published', 'rejected'].includes(receipt.state) ||
        remaining(receipt.nextCheckAt)
      )
        return receipt;
      // Reserve the cache interval before making a call, so failures and restarts cannot create a polling storm.
      receipt.nextCheckAt = Date.now() + 60000;
      await write(env.RECEIPTS, receiptKey(receipt.id), JSON.stringify(receipt));
      try {
        const limited = budgetedFetch(env.RECEIPTS, send);
        const token = await installationToken(env, limited);
        receipt.state = await reviewState(github(token, limited), receipt.number);
        if (receipt.state === 'publishing') {
          const response = await send(env.SITE_ORIGIN + '/community/catalog.json', {
            signal: AbortSignal.timeout(8000),
            cache: 'no-cache',
          });
          if (response.ok) {
            const catalog = (await response.json()) as {
              packages?: { id: string; version: string }[];
            };
            if (
              catalog.packages?.some(
                (p) => p.id === receipt.packageId && p.version === receipt.version,
              )
            )
              receipt.state = 'published';
          }
        }
      } catch (error) {
        if (error instanceof GithubError && error.status === 401) invalidateToken(env);
        if (error instanceof RetryLater) receipt.nextCheckAt = Date.now() + error.seconds * 1000;
        // Keep the last known state; a network failure is not a rejected submission.
      }
      await write(env.RECEIPTS, receiptKey(receipt.id), JSON.stringify(receipt));
      return receipt;
    });
  } catch (error) {
    if (error instanceof RetryLater)
      return { ...initial, nextCheckAt: Date.now() + error.seconds * 1000 };
    throw error;
  }
}
