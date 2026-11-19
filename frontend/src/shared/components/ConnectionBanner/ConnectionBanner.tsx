import { RefreshCw, WifiOff } from 'lucide-react';
import { Button } from '../Button';
import type { ConnectionBannerProps } from './type';

/**
 * Compact top-of-page alert shown ONLY when real-time updates drop or lag behind (Heuristics #1 & #9).
 * Stays invisible while connected so normal screens remain uncluttered.
 */
export function ConnectionBanner({ status, stale = false, refreshing = false, onRetry }: ConnectionBannerProps) {
  if (status !== 'reconnecting' && !stale) {
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
        <span>ข้อมูลอาจยังไม่ล่าสุด การเชื่อมต่อกำลังลองใหม่</span>
      </span>
      <Button variant="secondary" size="desk" disabled={refreshing} onClick={onRetry}>
        <RefreshCw aria-hidden="true" className="size-4" />
        {refreshing ? 'กำลังอัปเดต...' : 'ลองใหม่'}
      </Button>
    </div>
  );
}
