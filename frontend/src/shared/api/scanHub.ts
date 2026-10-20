import { HubConnectionBuilder } from '@microsoft/signalr';
import type { Dispatch } from '@reduxjs/toolkit';
import { hubFollowEnded, hubStatusChanged, type HubStatus } from '../../store/slices/hubSlice';
import type { AttendanceChanged, PointCard } from './types';

/** facility-0064: the automatic reconnect's waits; after it gives up, a fresh start every 3 s. */
export const HUB_RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
export const HUB_RESTART_MS = 3_000;

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
}: ScanHubFollow): Promise<void> {
  try {
    await cacheDataLoaded;
  } catch {
    return; // the entry went before its first answer
  }
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/scan', { accessTokenFactory: accessToken })
    .withAutomaticReconnect(HUB_RETRY_DELAYS_MS)
    .configureLogging('warn')
    .build();
  let removed = false;
  let restartTimer: ReturnType<typeof setTimeout> | undefined;
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
    setStatus('live');
    onMissed(); // every start, the first included: pushes sent between the first answer and the connection going live were missed
  };

  if (onPoint) {
    for (const event of POINT_EVENTS) {
      connection.on(event, (point: PointCard) => onPoint(point));
    }
  }
  if (onAttendance) {
    connection.on('AttendanceRecorded', (change: AttendanceChanged) => onAttendance(change));
  }
  // A handler for each event this follower does not take: the admins group sends all three, and the client logs a
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

  setStatus('connecting');
  void start();
  await cacheEntryRemoved;
  removed = true;
  clearTimeout(restartTimer);
  dispatch(hubFollowEnded(key));
  await connection.stop();
}
