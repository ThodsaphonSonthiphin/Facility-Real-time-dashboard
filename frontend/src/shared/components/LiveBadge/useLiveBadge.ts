import { useOnlineStatus } from '../../hooks/useOnlineStatus';

/**
 * Returns whether the Admin corner connection badge should show.
 * Shown only when the browser actually loses network connectivity (`!isOnline`).
 */
export function useLiveBadge() {
  const isOnline = useOnlineStatus();
  return { isDisconnected: !isOnline };
}
