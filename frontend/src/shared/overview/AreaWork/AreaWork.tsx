import { Link } from 'react-router';
import { Note } from '../../components/Note';
import { linkClass } from '../../components/styles';
import { formatDayTime, formatRound } from '../../utils/thaiTime';
import { areaHref } from '../overviewContext';
import type { Availability, OverviewRoundResult } from '../types';
import type { AreaWorkProps } from './type';

const outcomeText: Record<OverviewRoundResult['outcome'], string> = {
  notDone: 'ยังไม่ส่งงาน',
  notInspected: 'ยังไม่ได้ตรวจ',
  notReworked: 'ยังไม่ได้แก้ไข',
  passed: 'ตรวจผ่าน',
  unresolved: 'ยังยืนยันผลไม่ได้',
};
const reasonText: Record<string, string> = {
  slotClosed: 'ปิดรับข้อมูลของกะ',
  superseded: 'รอบใหม่เริ่มแล้ว',
  configurationChanged: 'ยกเลิกรอบเมื่อการตั้งค่าเปลี่ยน',
};
const panelClass = 'flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4';
const roundLinkClass = `${linkClass} inline-flex min-h-9 items-center`;

function availabilityText(availability: Availability): string | undefined {
  if (availability.state === 'partial') return 'ข้อมูลยังไม่ครบ แสดงเฉพาะรายการที่ยืนยันได้';
  if (availability.state === 'notStarted') return 'กะนี้ยังไม่เริ่ม';
  if (availability.state === 'unavailable') return 'ไม่มีข้อมูลสำหรับช่วงที่เลือก';
}

export function AreaWork({ detail, selection, view }: AreaWorkProps) {
  const { area, availability, facts, points, rounds, selectedRoundKey } = detail;
  const selectedRound = rounds.find((round) => round.roundKey === selectedRoundKey);
  const selectedPoint = selectedRoundKey ? points.find((point) => point.roundKey === selectedRoundKey) : undefined;
  const primary = view === 'inspection' ? availability.inspection : availability.work;
  const mainNote = availabilityText(primary);
  const historyNote = availabilityText(availability.history);
  const issueNote = availabilityText(availability.issue);
  const submissionById = new Map(facts.submissions.map((fact) => [fact.id, fact]));
  const inspectionById = new Map(facts.inspections.map((fact) => [fact.id, fact]));
  const pointById = new Map(points.map((point) => [point.work.pointId, point]));
  const latestByPoint = new Map<number, (typeof facts.submissions)[number]>();
  for (const submission of facts.submissions) {
    const latest = latestByPoint.get(submission.pointId);
    const at = Date.parse(submission.submittedAt);
    const latestAt = latest ? Date.parse(latest.submittedAt) : -Infinity;
    if (!latest || at > latestAt || (at === latestAt && submission.id > latest.id)) {
      latestByPoint.set(submission.pointId, submission);
    }
  }
  const issueSubmissions = [...latestByPoint.values()].filter((fact) => fact.status === 'Issue');
  const person = (id: number) => facts.people.find((one) => one.id === id)?.displayName;

  const factLines = (submissionId?: number | null, inspectionId?: number | null) => {
    const submission = submissionId != null ? submissionById.get(submissionId) : undefined;
    const inspection = inspectionId != null ? inspectionById.get(inspectionId) : undefined;
    const cleaner = submission && person(submission.userId);
    const supervisor = inspection && person(inspection.supervisorId);
    const submissionLine = submissionId != null && (
      <p>
        งานที่ส่ง #{submissionId}
        {submission && <> · <time dateTime={submission.submittedAt}>{formatDayTime(submission.submittedAt)}</time></>}
        {cleaner && ` · ${cleaner}`}
      </p>
    );
    const inspectionLine = inspectionId != null && (
      <div>
        <p>
          ผลตรวจ #{inspectionId}
          {inspection && <> · {inspection.result === 'Passed' ? 'ตรวจผ่าน' : 'ต้องแก้ไข'} · <time dateTime={inspection.inspectedAt}>{formatDayTime(inspection.inspectedAt)}</time></>}
          {supervisor && ` · ${supervisor}`}
        </p>
        {inspection?.defect && <p className="wrap-break-word">ข้อบกพร่อง {inspection.defect}</p>}
      </div>
    );
    return view === 'inspection' ? <>{inspectionLine}{submissionLine}</> : <>{submissionLine}{inspectionLine}</>;
  };

  return (
    <>
      <section aria-labelledby="overview-area-work" className={panelClass}>
        <h2 id="overview-area-work" className="text-lg font-semibold">{view === 'inspection' ? 'ผลการตรวจ' : 'งานทำความสะอาด'}</h2>
        {mainNote && <Note tone="warn">{mainNote}</Note>}
        <dl className="flex flex-wrap gap-x-6 gap-y-2">
          {primary.state === 'complete' && (
            <div><dt>{view === 'inspection' ? 'จุดที่ตรวจแล้ว / จุดที่ต้องทำ' : 'จุดที่ส่งงาน / จุดที่ต้องทำ'}</dt>
              <dd className="font-semibold tabular-nums">
                {view === 'inspection' ? area.work.inspectedPoints : area.work.submittedPoints} / {area.work.duePoints}
              </dd>
            </div>
          )}
          {view === 'inspection' && availability.work.state === 'complete' && (
            <div><dt>จุดที่ส่งงาน / จุดที่ต้องทำ</dt>
              <dd className="font-semibold tabular-nums">{area.work.submittedPoints} / {area.work.duePoints}</dd>
            </div>
          )}
        </dl>
        {availability.work.state === 'complete' && points.length === 0 && <p>ไม่มีรอบทำความสะอาดในกะที่เลือก</p>}
        <ul className="divide-y divide-border">
          {points.map((point) => {
            const { work, name, code, start, end, roundKey, windowId } = point;
            const key = windowId != null ? `window:${windowId}` : roundKey ?? `${work.pointId}:${start ?? ''}:${end ?? ''}`;
            return (
              <li key={key} className="flex flex-col gap-1 py-3">
                <h3 className="font-semibold">{code ? `${code} · ${name}` : name}</h3>
                {start && end && <p>{work.isUpcoming ? 'รอบถัดไป' : 'รอบ'} {formatRound({ start, end })}</p>}
                {work.isOverdue && <p className="text-overdue">เลยเวลา</p>}
                {work.isDue && !work.isSubmitted && availability.work.state === 'complete' && <p>ยังไม่ส่งงาน</p>}
                {work.isSubmitted && !work.isInspected && availability.inspection.state === 'complete' && <p>รอตรวจ</p>}
                {factLines(work.submissionId, work.inspectionId)}
              </li>
            );
          })}
        </ul>
      </section>
      <section aria-labelledby="overview-area-issues" className={panelClass}>
        <h2 id="overview-area-issues" className="text-lg font-semibold">ปัญหาที่แจ้ง</h2>
        {issueNote && <Note tone="warn">{issueNote}</Note>}
        {availability.issue.state === 'complete' && issueSubmissions.length === 0 && <p>ไม่มีปัญหาที่แจ้ง</p>}
        {issueSubmissions.length > 0 && (
          <ul className="list-disc space-y-2 ps-5">
            {issueSubmissions.map((fact) => (
              <li key={fact.id} className="wrap-break-word">
                {pointById.get(fact.pointId)?.name ?? `จุด #${fact.pointId}`} · งานที่ส่ง #{fact.id}
                {' · '}<time dateTime={fact.submittedAt}>{formatDayTime(fact.submittedAt)}</time>
                {fact.issueTags && <p>{fact.issueTags}</p>}
                {fact.note && <p>{fact.note}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>
      <section aria-labelledby="overview-area-rounds" className={panelClass}>
        <h2 id="overview-area-rounds" className="text-lg font-semibold">ประวัติรอบของพื้นที่</h2>
        {historyNote && <Note tone="warn">{historyNote}</Note>}
        {rounds.length === 0 && availability.history.state === 'complete' && <p>ยังไม่มีรอบที่สรุปผล</p>}
        {rounds.length > 0 && (
          <ul className="list-disc ps-5">
            {rounds.map(({ roundKey, round, outcome }) => (
              <li key={roundKey}>
                <Link className={roundLinkClass}
                  to={areaHref(selection, area.areaId, view, roundKey)}
                  aria-current={roundKey === selectedRoundKey ? 'true' : undefined}
                >
                  {pointById.get(round.pointId)?.name ?? `จุด #${round.pointId}`} · {formatRound(round)} · {outcomeText[outcome]}
                </Link>
              </li>
            ))}
          </ul>
        )}
        {selectedRound && (
          <section aria-labelledby="overview-selected-round" className="flex flex-col gap-1 border-t border-border pt-3">
            <h3 id="overview-selected-round" className="font-bold">รอบที่เลือก {formatRound(selectedRound.round)}</h3>
            <p>{outcomeText[selectedRound.outcome]}</p>
            {selectedRound.isPartial && <Note tone="warn">หลักฐานของรอบที่เลือกยังไม่ครบ</Note>}
            {selectedRound.round.finalizedReason && (
              <p>เหตุผลที่สรุปรอบ {reasonText[selectedRound.round.finalizedReason] ?? selectedRound.round.finalizedReason}</p>
            )}
            {selectedRound.round.finalizedAtUtc && (
              <p>สรุปรอบเมื่อ <time dateTime={selectedRound.round.finalizedAtUtc}>{formatDayTime(selectedRound.round.finalizedAtUtc)}</time></p>
            )}
            {factLines(selectedRound.submissionId, selectedRound.inspectionId)}
            {selectedRound.unresolvedSubmissions?.map((fact) => (
              <p key={fact.id}>งานที่ยังยืนยันรอบไม่ได้ #{fact.id} · <time dateTime={fact.submittedAt}>{formatDayTime(fact.submittedAt)}</time></p>
            ))}
          </section>
        )}
        {selectedPoint?.start && selectedPoint.end && (
          <p>รอบที่เลือก {formatRound({ start: selectedPoint.start, end: selectedPoint.end })}</p>
        )}
      </section>
    </>
  );
}
