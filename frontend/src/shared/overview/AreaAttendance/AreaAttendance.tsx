import { EditedMark } from '../../components/Badge';
import { Note } from '../../components/Note';
import { actionStateLabel, eventLabel, transferStateLabel } from '../../utils/labels';
import { formatDayTime } from '../../utils/thaiTime';
import type { OverviewAttendance } from '../types';
import type { AreaAttendanceProps } from './type';

function CorrectionProvenance({ fact }: { fact: OverviewAttendance }) {
  const additionLabel = fact.source === 'SupervisorAdd' ? 'หัวหน้างานเติมเวลา'
    : fact.source === 'AdminAdd' ? 'แอดมินเติมเวลา' : undefined;
  const legacyAudit = fact.correctedAt ? { reason: fact.reason, recordedAt: fact.correctedAt } : undefined;
  const addition = fact.addition ?? (additionLabel && !fact.edit ? legacyAudit : undefined);
  const edit = fact.edit ?? (!additionLabel ? legacyAudit : undefined);
  const additionReason = fact.addition?.reason ?? (fact.edit ? undefined : fact.reason);
  const editReason = fact.edit ? fact.edit.reason : fact.reason;
  return (
    <>
      {additionLabel && (
        <p className="wrap-break-word text-text-2">
          {fact.source === 'AdminAdd' && <><EditedMark /> </>}
          {additionLabel}
          {additionReason && ` · ${additionReason}`}
          {addition && <> · <time dateTime={addition.recordedAt}>{formatDayTime(addition.recordedAt)}</time></>}
        </p>
      )}
      {(fact.edit || !additionLabel) && (
        <p className="wrap-break-word text-text-2">
          <EditedMark /> แอดมินแก้เวลา
          {editReason && ` · ${editReason}`}
          {edit && <> · <time dateTime={edit.recordedAt}>{formatDayTime(edit.recordedAt)}</time></>}
        </p>
      )}
    </>
  );
}

export function AreaAttendance({ detail }: AreaAttendanceProps) {
  const { area, availability, context, facts } = detail;
  const evidence = detail.entryEvidence.filter((fact) =>
    fact.areaId === area.areaId && fact.shiftDate === context.shiftDate && fact.shift === context.shift);
  const events = facts.attendance.filter((fact) => fact.areaId === area.areaId);
  const gaps = facts.attendanceGaps?.filter((gap) => gap.areaId === area.areaId
    && !events.some((fact) => fact.userId === gap.userId && fact.event === gap.event)) ?? [];
  const leaves = facts.attendanceLeaves ?? [];
  const leaveEffects = facts.attendanceLeaveEffects ?? [];
  const covers = facts.covers.filter((fact) => fact.areaId === area.areaId);
  const responsibility = detail.currentResponsibility?.areaId === area.areaId ? detail.currentResponsibility : undefined;
  const person = (id: number) => facts.people.find((one) => one.id === id)?.displayName ?? `แม่บ้าน #${id}`;

  if (availability.entry.state === 'notStarted' || availability.entry.state === 'unavailable') {
    return <Note tone="info">ยังไม่มีข้อมูลการเข้างานสำหรับช่วงที่เลือก</Note>;
  }

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {availability.entry.state === 'partial' && (
        <div className="lg:col-span-2"><Note tone="warn">ข้อมูลการเข้างานยังไม่ครบ แสดงเฉพาะรายการที่ยืนยันได้</Note></div>
      )}
      <section aria-labelledby="overview-area-entry" className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4">
        <h2 id="overview-area-entry" className="text-lg font-semibold">หลักฐานเข้าพื้นที่</h2>
        {evidence.length === 0
          ? <p className="text-text-2">ไม่พบหลักฐานเข้าพื้นที่ในข้อมูลที่แสดง</p>
          : (
            <ul className="list-disc space-y-1 ps-5">
              {evidence.map((fact, index) => (
                <li key={`${fact.kind}:${fact.recordedAt}:${fact.transferId ?? index}`}>
                  {fact.kind === 'transfer-arrival' ? 'ยืนยันถึงพื้นที่หลังย้าย' : 'ลงเวลาเข้าพื้นที่'}
                  {' · '}<time dateTime={fact.recordedAt}>{formatDayTime(fact.recordedAt)}</time>
                  {fact.transferId != null && ` · รายการย้าย #${fact.transferId}`}
                </li>
              ))}
            </ul>
          )}
      </section>
      <section aria-labelledby="overview-area-responsibility" className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4">
        <h2 id="overview-area-responsibility" className="text-lg font-semibold">ความรับผิดชอบจากการย้ายพื้นที่</h2>
        {responsibility ? (
          <div className="flex flex-col gap-1">
            <p>{person(responsibility.userId)} · {transferStateLabel[responsibility.state]}</p>
            {responsibility.lastConfirmedAreaId != null && <p>พื้นที่ล่าสุดที่ยืนยัน #{responsibility.lastConfirmedAreaId}</p>}
            {responsibility.confirmedAt && (
              <p>ยืนยันเวลา <time dateTime={responsibility.confirmedAt}>{formatDayTime(responsibility.confirmedAt)}</time></p>
            )}
          </div>
        ) : <p className="text-text-2">ไม่มีข้อมูลความรับผิดชอบจากการย้ายพื้นที่</p>}
      </section>
      <section aria-labelledby="overview-area-events" className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4">
        <h2 id="overview-area-events" className="text-lg font-semibold">เวลาที่บันทึกในพื้นที่</h2>
        {events.length === 0 && gaps.length === 0
          && <p className="text-text-2">ไม่พบรายการลงเวลาของพื้นที่นี้</p>}
        {(events.length > 0 || gaps.length > 0) && (
          <ul className="list-disc space-y-2 ps-5">
            {events.map((fact) => (
              <li key={fact.id}>
                <span>{person(fact.userId)} · {eventLabel[fact.event]} · </span>
                <time dateTime={fact.occurredAt}>{formatDayTime(fact.occurredAt)}</time>
                <span className="text-text-2"> · พื้นที่ #{fact.areaId}</span>
                {fact.edited && <CorrectionProvenance fact={fact} />}
              </li>
            ))}
            {gaps.map((fact) => (
              <li key={`gap:${fact.id}`}>
                {person(fact.userId)} · {eventLabel[fact.event]} · — ({actionStateLabel.skipped}) · พื้นที่ #{fact.areaId}
                {fact.reason && <p className="wrap-break-word text-text-2">{fact.reason}</p>}
                {fact.recordedAt && <p className="text-text-2">บันทึกการข้าม <time dateTime={fact.recordedAt}>{formatDayTime(fact.recordedAt)}</time></p>}
              </li>
            ))}
          </ul>
        )}
      </section>
      {(leaves.length > 0 || leaveEffects.length > 0) && (
        <section aria-labelledby="overview-area-leave" className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4">
          <h2 id="overview-area-leave" className="text-lg font-semibold">การลาของกะนี้</h2>
          <ul className="list-disc space-y-2 ps-5">
            {leaves.map((fact) => (
              <li key={fact.id}>
                {person(fact.userId)} · ช่วงลาที่บันทึก{' '}
                <time dateTime={fact.startsAt}>{formatDayTime(fact.startsAt)}</time>
                {'–'}<time dateTime={fact.endsAt}>{formatDayTime(fact.endsAt)}</time>
              </li>
            ))}
            {leaveEffects.map((effect) => (
              <li key={`${effect.userId}:${effect.event}:${effect.kind}`}>
                {person(effect.userId)} · {eventLabel[effect.event]} ·{' '}
                {effect.kind === 'exempt' ? actionStateLabel['not-applicable'] : 'ลงเวลาในช่วงลาที่บันทึก'}
              </li>
            ))}
          </ul>
        </section>
      )}
      <section aria-labelledby="overview-area-covers" className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4">
        <h2 id="overview-area-covers" className="text-lg font-semibold">การมอบหมายคนทำแทน</h2>
        {covers.length === 0
          ? <p className="text-text-2">ไม่มีรายการมอบหมายในข้อมูลที่แสดง</p>
          : (
            <ul className="list-disc space-y-1 ps-5">
              {covers.map((cover) => (
                <li key={cover.id}>
                  {person(cover.userId)} · มอบหมาย{' '}
                  <time dateTime={cover.assignedAt}>{formatDayTime(cover.assignedAt)}</time>
                  {cover.cancelledAt
                    ? <> · ยกเลิก <time dateTime={cover.cancelledAt}>{formatDayTime(cover.cancelledAt)}</time></>
                    : <> · {events.some((fact) => fact.userId === cover.userId && fact.event === 'ShiftIn')
                      ? 'มีเวลาลงเข้างานที่พื้นที่นี้'
                      : 'ไม่พบเวลาลงเข้างานของผู้ทำแทนที่พื้นที่นี้'}</>}
                </li>
              ))}
            </ul>
          )}
      </section>
    </div>
  );
}
