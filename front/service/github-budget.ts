import { read, update, RetryLater, remaining } from './storage.ts';
import type { Store } from './storage.ts';

interface Budget {
  start: number;
  count: number;
}
/** All visitors share the same GitHub allowance; an IP limit alone cannot protect it. */
export function budgetedFetch(store: Store, send: typeof fetch, allowance = 3): typeof fetch {
  const deadline = Date.now() + 60000;
  let reserved = false,
    used = 0;
  return async (input, init) => {
    if (Date.now() >= deadline) throw new RetryLater();
    const pause = await read<{ until: number }>(store, 'github/pause');
    if (remaining(pause?.until)) throw new RetryLater(remaining(pause?.until));
    if (used >= allowance) throw new RetryLater();
    if (!reserved)
      await update<Budget>(store, 'github/budget', (previous) => {
        const current =
          previous && Date.now() < previous.start + 3600000
            ? previous
            : { start: Date.now(), count: 0 };
        if (current.count + allowance > 3000)
          throw new RetryLater(remaining(current.start + 3600000));
        return { ...current, count: current.count + allowance };
      });
    reserved = true;
    used++;
    const response = await send(input, {
      ...init,
      signal: AbortSignal.any([
        AbortSignal.timeout(Math.max(1, deadline - Date.now())),
        ...(init?.signal ? [init.signal] : []),
      ]),
    });
    if (response.status === 429 || response.status === 403) {
      const retry = response.headers.get('Retry-After');
      const reset =
        response.headers.get('X-RateLimit-Remaining') === '0'
          ? Number(response.headers.get('X-RateLimit-Reset')) * 1000
          : 0;
      const delay = retry
        ? Number.isFinite(Number(retry))
          ? Date.now() + Number(retry) * 1000
          : Date.parse(retry)
        : 0;
      const until = Math.max(Date.now() + 60000, reset || 0, delay || 0);
      await update<{ until: number }>(store, 'github/pause', (old) => ({
        until: Math.max(old?.until ?? 0, until),
      }));
      throw new RetryLater(remaining(until));
    }
    return response;
  };
}
