import { useAppSelector } from '../../store/hooks';
import { selectHubStatus } from '../../store/slices/hubSlice';

/**
 * facility-0064 item 1: the Admin pages' corner badge, with today's words in the new look's colours: "SignalR Live"
 * in green while pushes arrive, "Connecting..." in orange otherwise. Meanwhile the poll keeps the page at most 30 s
 * old (facility-0061).
 */
export function LiveBadge({ hubKey }: { hubKey: string }) {
  const live = useAppSelector(selectHubStatus(hubKey)) === 'live';
  return (
    <span
      role="status"
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold whitespace-nowrap ${live ? 'border-border bg-surface text-status-passed' : 'border-warn-border bg-warn-bg text-warn-text'}`}
    >
      <span aria-hidden="true" className={`size-2 rounded-full ${live ? 'bg-status-passed' : 'bg-status-overdue'}`} />
      {live ? 'SignalR Live' : 'Connecting...'}
    </span>
  );
}
