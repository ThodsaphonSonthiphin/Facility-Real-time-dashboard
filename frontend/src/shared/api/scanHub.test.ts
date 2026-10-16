import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { point } from '../../test/cleanerFixtures';
import { hub } from '../../test/fakeSignalR';
import { followScanHub } from './scanHub';
import type { PointCard } from './types';

/** One cache entry's life, driven by hand: `loaded` is its first answer, `remove` its end. */
function follow() {
  const store = makeStore();
  let loaded: (value?: unknown) => void = () => {};
  let failLoad: (reason: unknown) => void = () => {};
  let remove: () => void = () => {};
  const cacheDataLoaded = new Promise((resolve, reject) => {
    loaded = resolve;
    failLoad = reject;
  });
  const cacheEntryRemoved = new Promise<void>((resolve) => {
    remove = resolve;
  });
  const points: PointCard[] = [];
  const counts = { missed: 0 };
  const done = followScanHub({
    cacheDataLoaded,
    cacheEntryRemoved,
    dispatch: store.dispatch,
    accessToken: async () => 'token-supervisor',
    onPoint: (card) => points.push(card),
    onMissed: () => {
      counts.missed += 1;
    },
  });
  return { loaded, failLoad, remove, points, counts, done, status: () => store.getState().hub.status };
}

describe('followScanHub (#26 hub-cache-bridge, facility-0064)', () => {
  it('connects to /hubs/scan only after the first answer, with a token, and goes live', async () => {
    const f = follow();
    await Promise.resolve();
    expect(hub.connections).toHaveLength(0);

    f.loaded();

    await vi.waitFor(() => expect(f.status()).toBe('live'));
    const connection = hub.latest();
    expect(connection.url).toBe('/hubs/scan');
    expect(connection.retryDelays).toEqual([0, 2_000, 5_000, 10_000, 30_000]);
    expect(connection.tokens).toEqual(['token-supervisor']);
    expect(f.counts.missed).toBe(1); // pushes between the first answer and going live
  });

  it('hands every ScanRecorded and InspectionRecorded card to the page, and nothing else', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));

    hub.latest().push('ScanRecorded', point({ id: 11, status: 'PendingInspection' }));
    hub.latest().push('InspectionRecorded', point({ id: 11, status: 'Passed' }));
    hub.latest().push('AttendanceRecorded', { id: 99 });

    expect(f.points.map((card) => card.status)).toEqual(['PendingInspection', 'Passed']);
  });

  it('after an automatic reconnect it asks once for what it missed', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));

    hub.latest().reconnecting();
    expect(f.status()).toBe('reconnecting');
    hub.latest().reconnected();

    expect(f.status()).toBe('live');
    expect(f.counts.missed).toBe(2); // 1 from the first start, 1 from the reconnect
  });

  it('when the connection closes it starts again every 3 s, then asks for what it missed', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      hub.failNextStarts = 1;
      hub.latest().close();
      expect(f.status()).toBe('reconnecting');

      await vi.advanceTimersByTimeAsync(2_999);
      expect(hub.latest().starts).toBe(1);
      await vi.advanceTimersByTimeAsync(1); // the first restart, which fails
      await vi.waitFor(() => {
        expect(hub.latest().starts).toBe(2);
        expect(vi.getTimerCount()).toBe(1); // the next restart is waiting
      });
      expect(f.status()).toBe('reconnecting');

      // vi.waitFor and the real clock have already moved the fake clock a little, so this edge has a 100 ms margin
      await vi.advanceTimersByTimeAsync(2_900);
      expect(hub.latest().starts).toBe(2);
      await vi.advanceTimersByTimeAsync(100); // the second restart gets through
      await vi.waitFor(() => expect(f.status()).toBe('live'));
      expect(hub.latest().starts).toBe(3);
      expect(f.counts.missed).toBe(2); // 1 from the first start, 1 from this restart
    } finally {
      vi.useRealTimers();
    }
  });

  it('a first start that fails is tried again in 3 s, and then asks for what it missed', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      hub.failNextStarts = 1;
      const f = follow();
      f.loaded();
      await vi.waitFor(() => {
        expect(f.status()).toBe('reconnecting');
        expect(vi.getTimerCount()).toBe(1);
      });
      expect(f.counts.missed).toBe(0);

      // vi.waitFor has already moved the fake clock a little, so this edge has a 100 ms margin
      await vi.advanceTimersByTimeAsync(2_900);
      expect(hub.latest().starts).toBe(1);
      await vi.advanceTimersByTimeAsync(100);

      await vi.waitFor(() => expect(f.status()).toBe('live'));
      expect(hub.latest().starts).toBe(2);
      expect(f.counts.missed).toBe(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it('when the entry goes, the connection stops, the status is connecting again, and nothing restarts', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      f.remove();
      await f.done;

      expect(hub.latest().stops).toBe(1);
      expect(f.status()).toBe('connecting');
      expect(vi.getTimerCount()).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('when the entry goes while a restart is waiting, that restart never happens', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      hub.failNextStarts = 1;
      hub.latest().close();
      await vi.advanceTimersByTimeAsync(3_000); // the first restart fails
      await vi.waitFor(() => {
        expect(hub.latest().starts).toBe(2);
        expect(vi.getTimerCount()).toBe(1); // the next restart is waiting
      });

      f.remove();
      await f.done;
      await vi.advanceTimersByTimeAsync(10_000);

      expect(hub.latest().starts).toBe(2);
      expect(vi.getTimerCount()).toBe(0);
      expect(f.status()).toBe('connecting');
    } finally {
      vi.useRealTimers();
    }
  });

  it('an entry removed before its first answer never connects', async () => {
    const f = follow();
    f.failLoad(new Error('removed before the first answer'));
    await f.done;

    expect(hub.connections).toHaveLength(0);
  });
});
