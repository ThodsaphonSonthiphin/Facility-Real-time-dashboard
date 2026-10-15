import type { PointStatus } from '../api/types';
import { pointStatusLabel } from '../utils/labels';
import './shared.css';

/** A point's status (facility-0047 rule 4) as the same coloured chip on every page. */
export function StatusChip({ status }: { status: PointStatus }) {
  return <span className={`app-chip app-chip--${status}`}>{pointStatusLabel[status]}</span>;
}
