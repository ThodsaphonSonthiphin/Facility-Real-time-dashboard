import { Link } from 'react-router';
import { StatusChip } from '../../shared/components/StatusChip';
import { formatRound, formatTime } from '../../shared/utils/thaiTime';
import type { InspectionRecorded, InspectionVisit } from './inspectApi';
import type { InspectDraft } from './inspectRules';

interface InspectResultProps {
  visit: InspectionVisit;
  draft: InspectDraft;
  result: InspectionRecorded;
}

/** Wireframe M7 (facility-0029): the verdict is saved; ไม่สะอาด puts the defect on the Cleaner's card at once. */
export function InspectResult({ visit, draft, result }: InspectResultProps) {
  const { point } = visit;
  const cleaner = visit.submission?.cleanerName ?? 'แม่บ้าน';
  return (
    <main className="inspect-page">
      <header className="inspect-heading">
        <span className="inspect-kicker">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
        <h1>{`บันทึกผลตรวจแล้ว ${formatTime(result.inspectedAt)}`}</h1>
        <p className="inspect-sub">
          {[point.name, point.currentRound ? `รอบ ${formatRound(point.currentRound)}` : null].filter(Boolean).join(' · ')}
        </p>
      </header>
      {result.result === 'Passed' ? (
        <section className="app-note app-note--info">
          <p className="inspect-verdict">
            <strong>ผล: สะอาด</strong>
            <StatusChip status={result.newPointStatus} />
          </p>
        </section>
      ) : (
        <section className="app-note app-note--error">
          <p className="inspect-verdict">
            <strong>ผล: ไม่สะอาด</strong>
            <StatusChip status={result.newPointStatus} />
          </p>
          <span>{`“${draft.defect.trim()}”`}</span>
          <span>{`${cleaner}เห็นสิ่งที่ต้องแก้นี้ทันทีในหน้างานของฉัน และเมื่อสแกนจุดนี้ แก้แล้วส่งงานใหม่ จากนั้นหัวหน้าตรวจอีกรอบ`}</span>
          {point.nextRound && (
            <span>{`ถ้าไม่ได้แก้จนรอบ ${formatRound(point.nextRound)} เริ่ม การ์ดขึ้นรอบใหม่ และรอบนี้บันทึกว่า "ไม่ได้แก้"`}</span>
          )}
        </section>
      )}
      {result.withinRadius === false && (
        <p className="app-note app-note--warn">{`ไกลป้าย: ห่างป้าย ${result.distanceM} ม. แอดมินจะเห็นรายการนี้`}</p>
      )}
      {result.withinRadius === true && <p className="inspect-muted">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>}
      <Link className="app-button app-button--primary" to="/my-building">
        กลับไปตึกของฉัน
      </Link>
    </main>
  );
}
