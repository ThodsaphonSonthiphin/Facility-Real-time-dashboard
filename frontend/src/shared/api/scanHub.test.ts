import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { selectHubStatus } from '../../store/slices/hubSlice';
import { point } from '../../test/cleanerFixtures';
import { hub } from '../../test/fakeSignalR';
import { followScanHub } from './scanHub';
import type { AttendanceChanged, PointCard } from './types';

interface FollowOptions {
  /** The connection's key in hubSlice. */
  key?: string;
  /** Whether the page takes point cards (ScanRecorded, InspectionRecorded). */
  points?: boolean;
  /** Whether the page takes AttendanceRecorded. */
  attendance?: boolean;
}

/** One cache entry's life, driven by hand: `loaded` is its first answer, `remove` its end. */
function follow({ key = 'test', points: withPoints = true, attendance: withAttendance = false }: FollowOptions = {}, store = makeStore()) {
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
  const changes: AttendanceChanged[] = [];
  const counts = { missed: 0 };
  const done = followScanHub({
    key,
    cacheDataLoaded,
    cacheEntryRemoved,
    dispatch: store.dispatch,
    accessToken: async () => 'token-supervisor',
    onPoint: withPoints ? (card) => points.push(card) : undefined,
    onAttendance: withAttendance ? (change) => changes.push(change) : undefined,
    onMissed: () => {
      counts.missed += 1;
    },
  });
  return {
    loaded,
    failLoad,
    remove,
    points,
    changes,
    counts,
    done,
    status: () => selectHubStatus(key)(store.getState()),
    connections: () => store.getState().hub.connections,
  };
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

  it('hands AttendanceRecorded to a page that asked for it, and no card to a page that did not (facility-0027 rule 5)', async () => {
    const f = follow({ points: false, attendance: true });
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));

    hub.latest().push('ScanRecorded', point());
    hub.latest().push('AttendanceRecorded', { areaId: 1, shiftDate: '2026-10-15', shift: 'Day' });

    expect(f.points).toEqual([]);
    expect(f.changes).toEqual([{ areaId: 1, shiftDate: '2026-10-15', shift: 'Day' }]);
  });

  it('gives the server no answer even when the page handler returns one (plan 11 real run: D2 logged an Error per push)', async () => {
    const store = makeStore();
    followScanHub({
      key: 'answers',
      cacheDataLoaded: Promise.resolve(),
      cacheEntryRemoved: new Promise<void>(() => {}),
      dispatch: store.dispatch,
      accessToken: async () => 'token-admin',
      // Like attendanceBoardApi's `(change) => dispatch(...)`, which returns the action
      onPoint: (card) => ({ type: 'returned', card }) as unknown as void,
      onAttendance: (change) => ({ type: 'returned', change }) as unknown as void,
      onMissed: () => {},
    });
    await vi.waitFor(() => expect(selectHubStatus('answers')(store.getState())).toBe('live'));

    expect(() => hub.latest().push('ScanRecorded', point())).not.toThrow();
    expect(() => hub.latest().push('AttendanceRecorded', { areaId: 1, shiftDate: '2026-10-15', shift: 'Day' })).not.toThrow();
  });

  it('each connection keeps its own status, and an ended one leaves no status behind (plan 11 decision 4)', async () => {
    const store = makeStore();
    const a = follow({ key: 'a' }, store);
    a.loaded();
    await vi.waitFor(() => expect(a.status()).toBe('live'));
    hub.failNextStarts = 1;
    const b = follow({ key: 'b' }, store);
    b.loaded();
    await vi.waitFor(() => expect(b.status()).toBe('reconnecting'));

    expect(a.status()).toBe('live');
    a.remove();
    await a.done;
    expect(store.getState().hub.connections).toEqual({ b: 'reconnecting' });

    b.remove();
    await b.done;
    expect(store.getState().hub.connections).toEqual({});
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

  it('when the entry goes, the connection stops, its status goes, and nothing restarts', async () => {
    const f = follow();
    f.loaded();
    await vi.waitFor(() => expect(f.status()).toBe('live'));
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      f.remove();
      await f.done;

      expect(hub.latest().stops).toBe(1);
      expect(f.connections()).toEqual({}); // the ended connection leaves no status behind
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
      expect(f.connections()).toEqual({}); // the ended connection leaves no status behind
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
