import type { Dispatch } from '@reduxjs/toolkit';
import { hubFollowEnded, hubStatusChanged, type HubStatus } from '../../store/slices/hubSlice';
import type { AttendanceChanged, PointCard } from './types';

/** facility-0064: the automatic reconnect's waits; after it gives up, a fresh start every 3 s. */
export const HUB_RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
export const HUB_RESTART_MS = 3_000;
/**
 * facility-0107: an Admin page counts the link lost after 10 s without a word from the server (the client's own is
 * 30 s); the server pings every 5 s. Phones keep 30 s: a factory's signal drops for a moment and the badge would flicker.
 */
export const ADMIN_HUB_SERVER_TIMEOUT_MS = 10_000;
/**
 * facility-0107: back online, the page asks the server after this long. Chromium says online before a request gets
 * through (E2E 2026-11-03: the ask at once failed with net::ERR_INTERNET_DISCONNECTED, and the next was the 30 s poll).
 */
export const HUB_ONLINE_ASK_MS = 1_000;

/** ScanHub's point events (facility-0057, 0058): each carries the point's whole card as the clock's shift sees it. */
const POINT_EVENTS = ['ScanRecorded', 'InspectionRecorded'] as const;

export interface ScanHubFollow {
  /** This connection's name in hubSlice (plan 11 decision 4); the page's badge or footer reads the same key. */
  key: string;
  /** From onCacheEntryAdded: connect once the first answer is in, stop when the entry goes. */
  cacheDataLoaded: Promise<unknown>;
  cacheEntryRemoved: Promise<unknown>;
  dispatch: Dispatch;
  /** Asked on every connect and reconnect (facility-0023 item 3). */
  accessToken: () => Promise<string>;
  /** ScanRecorded and InspectionRecorded; left out by a page that shows no point cards. */
  onPoint?: (point: PointCard) => void;
  /** AttendanceRecorded, sent to Admins only (facility-0027 rule 5); left out by a page that shows no attendance. */
  onAttendance?: (change: AttendanceChanged) => void;
  /** Pushes may have been missed while the connection was down: ask the server again (facility-0064 item 2). */
  onMissed: () => void;
  /** How long to wait to hear from the server before the link counts as lost; left out, the client's own 30 s. */
  serverTimeoutMs?: number;
}

/**
 * #26 hub-cache-bridge: one connection per cache entry, opened in onCacheEntryAdded, so StrictMode and remounts never
 * open a second one. The hub sends a point's whole card, so the page patches its cache instead of asking again.
 */
export async function followScanHub({
  key,
  cacheDataLoaded,
  cacheEntryRemoved,
  dispatch,
  accessToken,
  onPoint,
  onAttendance,
  onMissed,
  serverTimeoutMs,
}: ScanHubFollow): Promise<void> {
  // The SignalR client is a third of the main JS and no page needs it before its first answer, so it comes in its own
  // file, fetched while that answer is on its way (2026-10-28, measured: a phone's first screen no longer waits for it)
  const signalR = import('@microsoft/signalr');
  signalR.catch(() => {}); // handled below; an entry that goes first must not leave an unhandled rejection
  try {
    await cacheDataLoaded;
  } catch {
    return; // the entry went before its first answer
  }
  let entryGone = false;
  void cacheEntryRemoved.then(() => {
    entryGone = true;
  });
  let HubConnectionBuilder: typeof import('@microsoft/signalr').HubConnectionBuilder;
  let HubConnectionState: typeof import('@microsoft/signalr').HubConnectionState;
  try {
    ({ HubConnectionBuilder, HubConnectionState } = await signalR);
  } catch {
    // The file did not arrive (a new build replaced it, or the link dropped): the page keeps its polling
    dispatch(hubStatusChanged({ key, status: 'reconnecting' }));
    await cacheEntryRemoved;
    dispatch(hubFollowEnded(key));
    return;
  }
  if (entryGone) {
    return; // the entry went while the file was on its way
  }
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/scan', { accessTokenFactory: accessToken })
    .withAutomaticReconnect(HUB_RETRY_DELAYS_MS)
    .configureLogging('warn')
    .build();
  if (serverTimeoutMs !== undefined) {
    connection.serverTimeoutInMilliseconds = serverTimeoutMs;
  }
  let removed = false;
  let restartTimer: ReturnType<typeof setTimeout> | undefined;
  let onlineAskTimer: ReturnType<typeof setTimeout> | undefined;
  const setStatus = (status: HubStatus) => dispatch(hubStatusChanged({ key, status }));

  const restartLater = () => {
    if (!removed) {
      setStatus('reconnecting');
      restartTimer = setTimeout(() => void start(), HUB_RESTART_MS);
    }
  };
  const start = async () => {
    if (removed) {
      return;
    }
    try {
      await connection.start();
    } catch {
      restartLater();
      return;
    }
    if (removed) {
      void connection.stop(); // the entry went while this start was in flight: do not leave the connection open
      return;
    }
    if (!navigator.onLine) {
      setStatus('reconnecting'); // the network went while the start was finishing; back online asks again (facility-0107)
      return;
    }
    setStatus('live');
    onMissed(); // every start, the first included: pushes sent between the first answer and the connection going live were missed
  };

  // Block bodies: a push expects no answer, and the client logs an Error when a handler returns one (a page's
  // `(change) => dispatch(...)` returns the action)
  if (onPoint) {
    for (const event of POINT_EVENTS) {
      connection.on(event, (point: PointCard) => {
        onPoint(point);
      });
    }
  }
  if (onAttendance) {
    connection.on('AttendanceRecorded', (change: AttendanceChanged) => {
      onAttendance(change);
    });
  }
  // A handler for each event this follower does not take: an Admin gets all three, and the client logs a
  // warning for every message with no handler
  if (!onPoint) {
    for (const event of POINT_EVENTS) {
      connection.on(event, () => {});
    }
  }
  if (!onAttendance) {
    connection.on('AttendanceRecorded', () => {});
  }
  connection.onreconnecting(() => setStatus('reconnecting'));
  connection.onreconnected(() => {
    setStatus('live');
    onMissed();
  });
  connection.onclose(restartLater);
  // facility-0107: the browser knows at once when its own network goes, long before the connection's wait runs out.
  // Back online, the page asks the server soon (HUB_ONLINE_ASK_MS): a connection that dropped meanwhile may wait up to
  // 30 s for its next try (HUB_RETRY_DELAYS_MS), and until it is back the badge stays. One that never noticed is live.
  const wentOffline = () => setStatus('reconnecting');
  const cameOnline = () => {
    if (connection.state === HubConnectionState.Connected) {
      setStatus('live');
    }
    clearTimeout(onlineAskTimer);
    onlineAskTimer = setTimeout(onMissed, HUB_ONLINE_ASK_MS);
  };
  window.addEventListener('offline', wentOffline);
  window.addEventListener('online', cameOnline);

  setStatus('connecting');
  void start();
  await cacheEntryRemoved;
  removed = true;
  window.removeEventListener('offline', wentOffline);
  window.removeEventListener('online', cameOnline);
  clearTimeout(restartTimer);
  clearTimeout(onlineAskTimer);
  dispatch(hubFollowEnded(key));
  await connection.stop();
}
