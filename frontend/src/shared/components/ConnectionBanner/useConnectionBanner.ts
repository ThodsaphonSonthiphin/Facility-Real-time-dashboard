import { useOnlineStatus } from '../../hooks/useOnlineStatus';

/**
 * Returns whether the top-of-page connection banner should be displayed.
 * Shown only when the browser actually loses network connectivity (`!isOnline`).
 */
export function useConnectionBanner() {
  const isOnline = useOnlineStatus();
  return { isDisconnected: !isOnline };
}
