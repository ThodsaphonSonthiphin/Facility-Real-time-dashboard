import { List } from 'lucide-react';
import { Link } from 'react-router';
import type { PointCard } from '../../shared/api/types';
import { Panel } from '../../shared/components/Boxes';
import { Note } from '../../shared/components/Note';
import { PhoneScreen } from '../../shared/components/PhoneScreen';
import { StatusChip } from '../../shared/components/StatusChip';
import { buttonClass, noteClass } from '../../shared/components/styles';
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
    <PhoneScreen
      header={<InspectHeading visit={visit} />}
      bottom={
        <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-building">
          <List aria-hidden="true" className="size-6" />
          กลับไปตึกของฉัน
        </Link>
      }
    >
      {notice && (
        <Note tone="warn" status>
          {notice}
        </Note>
      )}
      <section aria-labelledby="inspect-nothing-title" className={`flex flex-col gap-0.5 ${noteClass('info')}`}>
        <h2 id="inspect-nothing-title" className="text-lg font-bold">
          {visit.refusal}
        </h2>
        <span>ตรวจได้เฉพาะงานที่ส่งในรอบปัจจุบัน</span>
      </section>
      <Panel title="จุดนี้ตอนนี้">
        <p className="flex flex-wrap items-center gap-2">
          <StatusChip status={point.status} />
          <span>{roundLine(point)}</span>
        </p>
        {point.rework && <p>{`ให้แก้ ${formatTime(point.rework.requestedAt)}: “${point.rework.defect}”`}</p>}
      </Panel>
      <DistanceLine visit={visit} />
      <p className="text-center text-sm text-text-2">ไม่มีปุ่มตรวจจนกว่าแม่บ้านจะส่งงานในรอบนี้</p>
    </PhoneScreen>
  );
}

function roundLine(point: PointCard): string {
  if (point.currentRound) {
    return `รอบปัจจุบัน ${formatRound(point.currentRound)}`;
  }
  return point.nextRound ? `รอบถัดไป ${formatRound(point.nextRound)}` : 'ตอนนี้จุดนี้ไม่มีรอบ';
}
