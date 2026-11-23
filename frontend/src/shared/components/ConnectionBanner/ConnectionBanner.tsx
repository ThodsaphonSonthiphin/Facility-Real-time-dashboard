import { RefreshCw, WifiOff } from 'lucide-react';
import { Button } from '../Button';
import type { ConnectionBannerProps } from './type';
import { useConnectionBanner } from './useConnectionBanner';

/**
 * Compact top-of-page alert shown ONLY when the browser connection is lost (offline).
 * Stays invisible while online so normal screens remain uncluttered.
 */
export function ConnectionBanner({ refreshing = false, onRetry }: ConnectionBannerProps) {
  const { isDisconnected } = useConnectionBanner();
  if (!isDisconnected) {
    return null;
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-overdue-border bg-overdue-bg px-3.5 py-2.5 text-sm text-text"
    >
      <span className="flex min-w-0 flex-1 items-center gap-2 font-semibold text-overdue">
        <WifiOff aria-hidden="true" className="size-4 shrink-0" />
        <span>ขาดการเชื่อมต่อ</span>
      </span>
      <Button variant="secondary" size="desk" disabled={refreshing} onClick={onRetry}>
        <RefreshCw aria-hidden="true" className="size-4" />
        {refreshing ? 'กำลังอัปเดต...' : 'ลองใหม่'}
      </Button>
    </div>
  );
}
