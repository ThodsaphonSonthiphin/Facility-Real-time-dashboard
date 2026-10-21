import { useAppSelector } from '../../store/hooks';
import { selectHubStatus } from '../../store/slices/hubSlice';
import './shared.css';

/**
 * facility-0064 item 1: the Admin pages' corner badge, with today's words and colours: "SignalR Live" in green while
 * pushes arrive, "Connecting..." in orange otherwise. Meanwhile the poll keeps the page at most 30 s old (facility-0061).
 */
export function LiveBadge({ hubKey }: { hubKey: string }) {
  const live = useAppSelector(selectHubStatus(hubKey)) === 'live';
  return (
    <span className={live ? 'app-live app-live--on' : 'app-live'} role="status">
      {live ? 'SignalR Live' : 'Connecting...'}
    </span>
  );
}
