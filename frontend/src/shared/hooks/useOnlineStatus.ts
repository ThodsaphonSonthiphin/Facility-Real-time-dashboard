import { useSyncExternalStore } from 'react';

function readNavigatorOnLine(): boolean {
  return typeof navigator === 'undefined' ? true : navigator.onLine;
}

let override: boolean | null = null;
let subscriberCount = 0;

function getSnapshot(): boolean {
  return override ?? readNavigatorOnLine();
}

function getServerSnapshot(): boolean {
  return true;
}

function subscribe(onStoreChange: () => void): () => void {
  if (subscriberCount === 0) {
    override = null;
  }
  subscriberCount += 1;

  const goOnline = () => {
    override = true;
    onStoreChange();
  };
  const goOffline = () => {
    override = false;
    onStoreChange();
  };

  window.addEventListener('online', goOnline);
  window.addEventListener('offline', goOffline);
  return () => {
    window.removeEventListener('online', goOnline);
    window.removeEventListener('offline', goOffline);
    subscriberCount -= 1;
    if (subscriberCount === 0) {
      override = null;
    }
  };
}

/**
 * Tracks whether the browser currently has network connectivity using `navigator.onLine`
 * and the `online` / `offline` window events via `useSyncExternalStore`.
 */
export function useOnlineStatus(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
