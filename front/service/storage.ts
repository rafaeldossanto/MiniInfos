export interface Store {
  get(key: string): Promise<{ etag: string; json<T>(): Promise<T> } | null>;
  put(key: string, value: string, options?: { onlyIf: Headers }): Promise<{ etag: string } | null>;
}
export class RetryLater extends Error {
  readonly seconds: number;
  constructor(seconds = 60) {
    super('Please retry later');
    this.seconds = Math.max(1, Math.ceil(seconds));
  }
}
export const remaining = (until = 0) => Math.max(0, Math.ceil((until - Date.now()) / 1000));
export async function read<T>(store: Store, key: string): Promise<T | null> {
  return (await store.get(key))?.json<T>() ?? null;
}
/** R2 may throttle successive writes to one key; keep the original CAS condition on retry. */
export async function write(
  store: Store,
  key: string,
  value: string,
  options?: { onlyIf: Headers },
) {
  for (let attempt = 0; ; attempt++) {
    try {
      return await store.put(key, value, options);
    } catch (error) {
      if (!(error instanceof Error) || !/\(10058\)\s*$/.test(error.message)) throw error;
      if (attempt >= 2) throw new RetryLater();
      await new Promise((resolve) => setTimeout(resolve, 1100 * 2 ** attempt));
    }
  }
}
export async function update<T>(store: Store, key: string, change: (value: T | null) => T) {
  for (let attempt = 0; attempt < 8; attempt++) {
    const previous = await store.get(key);
    const value = change(previous ? await previous.json<T>() : null);
    const saved = await write(store, key, JSON.stringify(value), {
      onlyIf: new Headers(previous ? { 'If-Match': previous.etag } : { 'If-None-Match': '*' }),
    });
    if (saved) return { value, etag: saved.etag };
  }
  throw new RetryLater();
}
/** Conditional R2 writes serialize requests across isolates, not just within one Worker. */
export async function withLease<T>(store: Store, key: string, run: () => Promise<T>): Promise<T> {
  const lock = 'locks/' + key;
  const claimed = await update<{ until: number; owner: string }>(store, lock, (previous) => {
    if (remaining(previous?.until)) throw new RetryLater(remaining(previous?.until));
    return { until: Date.now() + 120000, owner: crypto.randomUUID() };
  });
  try {
    return await run();
  } finally {
    // Never release a lease that was taken over after expiry.
    await write(store, lock, JSON.stringify({ ...claimed.value, until: 0 }), {
      onlyIf: new Headers({ 'If-Match': claimed.etag }),
    }).catch(() => {
      /* A failed release expires naturally; do not hide a saved result. */
    });
  }
}
