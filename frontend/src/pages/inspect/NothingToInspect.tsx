import { Link } from 'react-router';
import type { PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatRound, formatTime } from '../../shared/utils/thaiTime';
import { DistanceLine } from './DistanceLine';
import { InspectHeading } from './InspectHeading';
import type { InspectionVisit } from './inspectApi';

/**
 * Wireframe M16 (facility-0047 rule 7, facility-0029): no submission in the current round, or a rework still owed. The
 * server's reason is the heading; there is no inspect button. The day's round table is left out: the point carries
 * only its current and next round (plan 9 decision 7).
 */
export function NothingToInspect({ visit, notice }: { visit: InspectionVisit; notice: string | null }) {
  const { point } = visit;
  return (
    <main className="inspect-page">
      <InspectHeading visit={visit} />
      {notice && (
        <p className="app-note app-note--warn" role="status">
          {notice}
        </p>
      )}
      <section className="app-note app-note--info" aria-labelledby="inspect-nothing-title">
        <h2 id="inspect-nothing-title">{visit.refusal}</h2>
        <span>ตรวจได้เฉพาะงานที่ส่งในรอบปัจจุบัน</span>
      </section>
      <section className="app-panel">
        <h2>จุดนี้ตอนนี้</h2>
        <p className="inspect-status">
          <StatusChip status={point.status} />
          <span>{roundLine(point)}</span>
        </p>
        {point.rework && <p>{`ให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}”`}</p>}
      </section>
      <DistanceLine visit={visit} />
      <Link className="app-button app-button--primary" to="/my-building">
        กลับไปตึกของฉัน
      </Link>
      <p className="inspect-muted">ไม่มีปุ่มตรวจจนกว่าแม่บ้านจะส่งงานในรอบนี้</p>
    </main>
  );
}

function roundLine(point: PointCard): string {
  if (point.currentRound) {
    return `รอบปัจจุบัน ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : 'ตอนนี้จุดนี้ไม่มีรอบ';
}
