import { Link } from 'react-router';
import type { Placement, PointCard } from '../../shared/api/types';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatMinutes, formatTime } from '../../shared/utils/thaiTime';
import type { ScanRecordCreated } from './scanApi';
import { describeDraft, type ScanDraft } from './scanRules';

const title: Record<Placement, string> = {
  OnTime: 'ส่งงานแล้ว',
  Late: 'ส่งงานแล้ว',
  Rework: 'ส่งงานแก้แล้ว',
  OffRound: 'บันทึกเป็นงานนอกรอบ',
};

interface SubmitResultProps {
  point: PointCard;
  draft: ScanDraft;
  result: ScanRecordCreated;
}

/** Wireframes M5 and M13: the server placed the submission (facility-0047 rule 3) and judged the distance (facility-0037). */
export function SubmitResult({ point, draft, result }: SubmitResultProps) {
  return (
    <main className="scan-page">
      <header className="scan-heading">
        <span className="scan-kicker">{`${point.areaName} · ตึก ${point.buildingCode}`}</span>
        <h1>{`${title[result.placement]} ${formatTime(result.submittedAt)}`}</h1>
        <p className="scan-sub">{`${point.name} · ${describeDraft(draft)}`}</p>
      </header>
      {result.placement === 'Late' && (
        <p className="app-note app-note--warn">
          <strong>{`ส่งช้า ${formatMinutes(result.lateMinutes ?? 0)}`}</strong>
          <span>รอบนี้จบแล้วและยังไม่มีใครส่ง จึงนับเป็นงานของรอบนี้ แต่ติดว่าส่งช้า</span>
        </p>
      )}
      {result.placement === 'Rework' && <p className="app-note app-note--info">นับเป็นงานแก้ของรอบนี้ หัวหน้าจะตรวจอีกครั้ง</p>}
      {result.placement === 'OffRound' && (
        <p className="app-note app-note--warn">
          ตอนนี้ไม่มีรอบไหนรับงานนี้ ระบบบันทึกไว้แล้ว แต่ไม่นับเป็นรอบใด และสถานะของจุดไม่เปลี่ยน
        </p>
      )}
      {result.withinRadius === false && (
        <p className="app-note app-note--warn">
          {`ติดธง: ห่างป้าย ${result.distanceM} ม. Admin จะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้ง Admin ได้เลย`}
        </p>
      )}
      {result.withinRadius === true && <p className="scan-muted">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>}
      <section className="app-panel">
        <h2>สถานะของจุดนี้</h2>
        <p>
          <StatusChip status={result.newPointStatus} />
        </p>
      </section>
      <Link className="app-button app-button--primary" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}
