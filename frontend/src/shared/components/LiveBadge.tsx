import { useAppSelector } from '../../store/hooks';
import { selectHubStatus } from '../../store/slices/hubSlice';

/**
 * facility-0064 item 1: the Admin pages' corner badge. Nothing shows while pushes arrive; an orange badge shows only
 * while they do not (owner, 2026-10-28: "SignalR Live" meant nothing to an Admin). Meanwhile the poll keeps the page
 * at most 30 s old (facility-0061). The status region stays in the page, empty, so a screen reader hears the change.
 * Not while the first connection is being made (owner, 2026-10-28): it flashed on every open and moved the page.
 */
export function LiveBadge({ hubKey }: { hubKey: string }) {
  const down = useAppSelector(selectHubStatus(hubKey)) === 'reconnecting';
  return (
    <span role="status">
      {down && (
        <span className="inline-flex items-center gap-1.5 rounded-full border border-warn-border bg-warn-bg px-2.5 py-1 text-xs font-semibold whitespace-nowrap text-warn-text">
          <span aria-hidden="true" className="size-2 rounded-full bg-status-overdue" />
          กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)
        </span>
      )}
    </span>
  );
}
