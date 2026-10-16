import { HubConnectionBuilder } from '@microsoft/signalr';
import type { Dispatch } from '@reduxjs/toolkit';
import { hubStatusChanged } from '../../store/slices/hubSlice';
import type { PointCard } from './types';

/** facility-0064: the automatic reconnect's waits; after it gives up, a fresh start every 3 s. */
export const HUB_RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
export const HUB_RESTART_MS = 3_000;

/** ScanHub's point events (facility-0057, 0058): each carries the point's whole card as the clock's shift sees it. */
const POINT_EVENTS = ['ScanRecorded', 'InspectionRecorded'] as const;

export interface ScanHubFollow {
  /** From onCacheEntryAdded: connect once the first answer is in, stop when the entry goes. */
  cacheDataLoaded: Promise<unknown>;
  cacheEntryRemoved: Promise<unknown>;
  dispatch: Dispatch;
  /** Asked on every connect and reconnect (facility-0023 item 3). */
  accessToken: () => Promise<string>;
  onPoint: (point: PointCard) => void;
  /** Pushes may have been missed while the connection was down: ask the server again (facility-0064 item 2). */
  onMissed: () => void;
}

/**
 * #26 hub-cache-bridge: one connection per cache entry, opened in onCacheEntryAdded, so StrictMode and remounts never
 * open a second one. The hub sends a point's whole card, so the page patches its cache instead of asking again.
 */
export async function followScanHub({
  cacheDataLoaded,
  cacheEntryRemoved,
  dispatch,
  accessToken,
  onPoint,
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

  const restartLater = () => {
    if (!removed) {
      dispatch(hubStatusChanged('reconnecting'));
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
    dispatch(hubStatusChanged('live'));
    onMissed(); // every start, the first included: pushes sent between the first answer and the connection going live were missed
  };

  for (const event of POINT_EVENTS) {
    connection.on(event, (point: PointCard) => onPoint(point));
  }
  connection.onreconnecting(() => dispatch(hubStatusChanged('reconnecting')));
  connection.onreconnected(() => {
    dispatch(hubStatusChanged('live'));
    onMissed();
  });
  connection.onclose(restartLater);

  dispatch(hubStatusChanged('connecting'));
  void start();
  await cacheEntryRemoved;
  removed = true;
  clearTimeout(restartTimer);
  dispatch(hubStatusChanged('connecting'));
  await connection.stop();
}
