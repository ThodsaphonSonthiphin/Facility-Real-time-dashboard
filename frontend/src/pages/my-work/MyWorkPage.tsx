import { freshOnOpen, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import type { AttendanceState, MyWork, PointStatus } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { WorkCard } from './WorkCard';
import './myWork.css';

/** What needs the Cleaner comes first (plan 8 decision 6), then what is done or not due yet. */
const urgency: readonly PointStatus[] = [
  'Rework',
  'Overdue',
  'NotYetDone',
  'PendingInspection',
  'Passed',
  'BeforeFirstRound',
  'OffHours',
];

/**
 * facility-0052, wireframes M9 and M14: every point of the Cleaner's own Area, then of the Areas they cover this
 * shift. No push and no hub: the page asks the server each time it opens and on "อัปเดต". No card links to the scan
 * page; the only way there is the sign's QR (facility-0059). The name is not shown: the bar above already shows it.
 */
export function MyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);

  return (
    <main className="mywork-page">
      <header className="mywork-heading">
        <h1>งานของฉัน</h1>
        {data && <p className="mywork-sub">{attendanceLine(data.attendance)}</p>}
      </header>
      {isLoading && <p className="mywork-muted">กำลังโหลดงาน...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && <WorkList work={data} />}
      <footer className="mywork-footer">
        <span>ไม่มีแจ้งเตือนเด้งขึ้น เปิดหน้านี้หรือกด "อัปเดต" เพื่อดูงานล่าสุด</span>
        <button type="button" className="app-button" disabled={isFetching} onClick={() => void refetch()}>
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </button>
      </footer>
    </main>
  );
}

function attendanceLine(attendance: AttendanceState): string {
  if (!attendance.shift) {
    return 'ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ';
  }
  const shiftIn = attendance.events.find((e) => e.eventType === 'ShiftIn');
  const shiftOut = attendance.events.find((e) => e.eventType === 'ShiftOut');
  return [
    shiftLabel[attendance.shift],
    shiftIn ? `เข้างาน ${formatTime(shiftIn.occurredAt)}` : 'ยังไม่ได้ลงเวลาเข้างาน',
    shiftOut ? `เลิกงาน ${formatTime(shiftOut.occurredAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

function WorkList({ work }: { work: MyWork }) {
  if (work.areas.length === 0) {
    return (
      <p className="app-note app-note--info">
        ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับ Area หรือ Area ยังไม่มีจุด แจ้ง Admin
      </p>
    );
  }
  const points = work.areas.flatMap((area) => area.points);
  const count = (...statuses: PointStatus[]) => points.filter((p) => statuses.includes(p.status)).length;
  const covered = work.areas.filter((area) => area.isCover);

  return (
    <>
      <ul className="mywork-summary" aria-label="สรุปงาน">
        <li className="mywork-summary__rework">
          <strong>{count('Rework')}</strong> ต้องแก้
        </li>
        <li className="mywork-summary__overdue">
          <strong>{count('Overdue')}</strong> เลยรอบ
        </li>
        <li>
          <strong>{count('PendingInspection', 'Passed')}</strong> ส่งแล้ว
        </li>
      </ul>
      {covered.length > 0 && (
        <p className="app-note app-note--info">
          {`วันนี้ทำแทน ${covered.map((area) => area.areaName).join(', ')} ด้วย · สิทธิ์หมดเองเมื่อหมดกะ`}
        </p>
      )}
      {work.areas.map((area) => {
        const headingId = `mywork-area-${area.areaId}`;
        const title = area.isCover
          ? `${area.areaName} · ตึก ${area.points[0]?.buildingCode ?? ''} (ทำแทน)`
          : `${area.areaName} (Area ของฉัน)`;
        const sorted = [...area.points].sort((a, b) => urgency.indexOf(a.status) - urgency.indexOf(b.status));
        return (
          <section key={area.areaId} className="mywork-area" aria-labelledby={headingId}>
            <h2 id={headingId}>{title}</h2>
            <ul className="mywork-cards">
              {sorted.map((point) => (
                <WorkCard key={point.id} point={point} />
              ))}
            </ul>
          </section>
        );
      })}
    </>
  );
}
