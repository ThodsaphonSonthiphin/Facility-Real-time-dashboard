import { Link } from 'react-router';
import { Note } from '../../shared/components/Note';
import { StatusChip } from '../../shared/components/StatusChip';
import { buttonClass, noteClass } from '../../shared/components/styles';
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
    <main className="flex flex-col gap-4 text-base text-text">
      <header className="flex flex-col gap-1.5">
        <span className="text-sm tracking-wide text-text-2">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
        <h1 className="text-2xl font-bold">{`บันทึกผลตรวจแล้ว ${formatTime(result.inspectedAt)}`}</h1>
        <p className="text-sm text-text-2">
          {[point.name, point.currentRound ? `รอบ ${formatRound(point.currentRound)}` : null].filter(Boolean).join(' · ')}
        </p>
      </header>
      {/* The verdict and the point's new status, large enough to read at arm's length (pages/phone.md) */}
      {result.result === 'Passed' ? (
        <section className={noteClass('info')}>
          <p className={verdictClass}>
            <strong>ผล: สะอาด</strong>
            <StatusChip status={result.newPointStatus} size="lg" />
          </p>
        </section>
      ) : (
        <section className={`flex flex-col gap-1 ${noteClass('error')}`}>
          <p className={verdictClass}>
            <strong>ผล: ไม่สะอาด</strong>
            <StatusChip status={result.newPointStatus} size="lg" />
          </p>
          <span>{`“${draft.defect.trim()}”`}</span>
          <span>{`${cleaner}เห็นสิ่งที่ต้องแก้นี้ทันทีในหน้างานของฉัน และเมื่อสแกนจุดนี้ แก้แล้วส่งงานใหม่ จากนั้นหัวหน้าตรวจอีกรอบ`}</span>
          {point.nextRound && (
            <span>{`ถ้าไม่ได้แก้จนรอบ ${formatRound(point.nextRound)} เริ่ม การ์ดขึ้นรอบใหม่ และรอบนี้บันทึกว่า "ไม่ได้แก้"`}</span>
          )}
        </section>
      )}
      {result.withinRadius === false && <Note tone="warn">{`ไกลป้าย: ห่างป้าย ${result.distanceM} ม. แอดมินจะเห็นรายการนี้`}</Note>}
      {result.withinRadius === true && <p className="text-sm text-text-2">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>}
      <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-building">
        กลับไปตึกของฉัน
      </Link>
    </main>
  );
}

const verdictClass = 'flex flex-wrap items-center gap-2 text-lg';
