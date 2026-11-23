import { WifiOff } from 'lucide-react';
import { useLiveBadge } from './useLiveBadge';

/**
 * Admin pages' corner connection badge. Nothing shows while online; an orange badge shows only while offline.
 * The status region stays in the page, empty, so a screen reader hears the change.
 */
export function LiveBadge() {
  const { isDisconnected } = useLiveBadge();

  return (
    <span role="status">
      {isDisconnected && (
        <span className="inline-flex min-h-9 items-center gap-2 rounded-md border border-overdue-border bg-overdue-bg px-3 text-sm font-semibold whitespace-nowrap text-overdue">
          <WifiOff aria-hidden="true" className="size-4 shrink-0" />
          ขาดการเชื่อมต่อ
        </span>
      )}
    </span>
  );
}
