import { Link } from 'react-router';
import type { Placement, PointCard } from '../../shared/api/types';
import { Note } from '../../shared/components/Note';
import { StatusChip } from '../../shared/components/StatusChip';
import { buttonClass } from '../../shared/components/styles';
import { formatMinutes, formatTime } from '../../shared/utils/thaiTime';
import type { ScanRecordCreated } from './scanApi';
import { describeDraft, type ScanDraft } from './scanRules';

const title: Record<Placement, string> = {
  OnTime: 'ส่งงานแล้ว',
  Late: 'ส่งงานแล้ว',
  Rework: 'ส่งงานแก้แล้ว',
  OffRound: 'ส่งเพิ่มแล้ว (ไม่นับรอบ)',
};

interface SubmitResultProps {
  point: PointCard;
  draft: ScanDraft;
  result: ScanRecordCreated;
}

/**
 * Wireframes M5 and M13: the server placed the submission (facility-0047 rule 3) and judged the distance
 * (facility-0037). The point's status is one line under the heading, not a card (facility-0079).
 */
export function SubmitResult({ point, draft, result }: SubmitResultProps) {
  return (
    <main className="flex flex-col gap-4 text-base text-text">
      <header className="flex flex-col gap-1.5">
        <span className="text-sm text-text-2">{`${point.areaName} · ตึก ${point.buildingCode}`}</span>
        <h1 className="text-2xl font-bold">{`${title[result.placement]} ${formatTime(result.submittedAt)}`}</h1>
        <p className="text-sm text-text-2">{`${point.name} · ${describeDraft(draft)}`}</p>
        <p className="flex flex-wrap items-center gap-2 text-lg">
          สถานะของจุดนี้ <StatusChip status={result.newPointStatus} size="lg" />
        </p>
      </header>
      {result.placement === 'Late' && (
        <Note tone="warn" title={`ส่งช้า ${formatMinutes(result.lateMinutes ?? 0)}`}>
          <span>รอบนี้จบแล้วและยังไม่มีใครส่ง จึงนับเป็นงานของรอบนี้ แต่ติดว่าส่งช้า</span>
        </Note>
      )}
      {result.placement === 'Rework' && <Note tone="info">นับเป็นงานแก้ของรอบนี้ หัวหน้าจะตรวจอีกครั้ง</Note>}
      {result.placement === 'OffRound' && (
        <Note tone="warn">ตอนนี้ไม่มีรอบไหนรับงานนี้ ระบบบันทึกไว้แล้ว แต่ไม่นับเป็นรอบใด และสถานะของจุดไม่เปลี่ยน</Note>
      )}
      {result.withinRadius === false && (
        <Note tone="warn">
          {`ไกลป้าย: ห่างป้าย ${result.distanceM} ม. แอดมินจะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้งแอดมินได้เลย`}
        </Note>
      )}
      {result.withinRadius === true && <p className="text-sm text-text-2">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>}
      <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}
