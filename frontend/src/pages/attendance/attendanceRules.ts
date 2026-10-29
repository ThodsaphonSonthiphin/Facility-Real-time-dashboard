import type {
  AreaAttendanceState,
  BoardArgs,
  BoardCover,
  BoardEvent,
  BoardRow,
} from '../../shared/api/attendanceBoardApi';
import type { AttendanceEvent, Shift } from '../../shared/api/types';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/** Wireframe D2's status column (facility-0050). */
export const areaStateLabel: Record<AreaAttendanceState, string> = {
  Empty: 'ยังไม่มีคน',
  Working: 'ทำงานอยู่',
  OnBreak: 'พักเบรก',
  Done: 'เลิกงานแล้ว',
  Covered: 'มีคนทำแทน',
};

const SHIFTS: readonly string[] = ['Day', 'Night'];

/**
 * What the address asks for: ?date=2026-10-15&shift=Night&building=2. A date without a shift, or the reverse, is left
 * out, because the server refuses one without the other; then the board is the clock's shift.
 */
export function readBoardArgs(params: URLSearchParams): BoardArgs {
  const args: BoardArgs = {};
  const date = params.get('date');
  const shift = params.get('shift');
  if (date && /^\d{4}-\d{2}-\d{2}$/.test(date) && shift && SHIFTS.includes(shift)) {
    args.shiftDate = date;
    args.shift = shift as Shift;
  }
  const building = Number(params.get('building'));
  if (Number.isInteger(building) && building > 0) {
    args.buildingId = building;
  }
  return args;
}

/** The address for a board, in the words readBoardArgs reads. */
export function toBoardParams({ shiftDate, shift, buildingId }: BoardArgs): URLSearchParams {
  const params = new URLSearchParams();
  if (shiftDate && shift) {
    params.set('date', shiftDate);
    params.set('shift', shift);
  }
  if (buildingId) {
    params.set('building', String(buildingId));
  }
  return params;
}

/** The rows a search keeps: the Area's code or name, its regular Cleaner, or a cover, in any letter case. */
export function searchRows(rows: readonly BoardRow[], search: string): BoardRow[] {
  const text = search.trim().toLowerCase();
  if (text === '') {
    return [...rows];
  }
  return rows.filter((row) =>
    [row.areaCode, row.areaName, row.cleaner?.displayName, ...row.covers.map((cover) => cover.displayName)].some((value) =>
      value?.toLowerCase().includes(text),
    ),
  );
}

/** The regular Cleaner's scan of that event, if there is one. */
export function eventOf(row: BoardRow, type: AttendanceEvent): BoardEvent | undefined {
  return row.events.find((event) => event.eventType === type);
}

/** The line under a cover's name and its ทำแทน badge (the design file's D2): their own Area, when assigned, their Shift-In. */
export function coverDetail(cover: BoardCover): string {
  return [
    cover.ownAreaCode ? `พื้นที่ของตัวเอง ${cover.ownAreaCode}` : null,
    `แอดมินมอบหมาย ${formatTime(cover.assignedAt)}`,
    cover.shiftInAt ? `เข้างาน ${formatTime(cover.shiftInAt)}` : 'ยังไม่ลงเวลา',
  ]
    .filter(Boolean)
    .join(' · ');
}

/** facility-0051: AdminAttendanceEndpoints.MaxReasonLength. */
export const MAX_REASON = 500;

/** The design file's reasons for a correction; อื่น ๆ needs the details written out. */
export const correctionReasons = ['มือถือแบตหมด', 'ไม่มีอินเทอร์เน็ต', 'ลืมลงเวลา', 'สแกนป้ายไม่ได้', 'อื่น ๆ'] as const;
export type CorrectionReason = (typeof correctionReasons)[number];
export const OTHER_REASON: CorrectionReason = 'อื่น ๆ';

/** The reason the server keeps in the history: the choice, then the details when there are any. */
export function correctionReason(choice: CorrectionReason, detail: string): string {
  const text = detail.trim();
  return text === '' ? choice : `${choice} · ${text}`;
}

/** What is wrong with the reason, checked before sending. */
export function reasonProblem(choice: CorrectionReason | null, detail: string): string | null {
  if (!choice) {
    return 'เลือกเหตุผลอย่างน้อย 1 ข้อ';
  }
  if (choice === OTHER_REASON && detail.trim() === '') {
    return 'เลือก "อื่น ๆ" ต้องเขียนรายละเอียด';
  }
  return correctionReason(choice, detail).length > MAX_REASON ? `เหตุผลยาวได้ไม่เกิน ${MAX_REASON} ตัวอักษร` : null;
}

const isClock = (text: string) => /^([01]\d|2[0-3]):[0-5]\d$/.test(text);

/** One time of the form: the one recorded ('' when none) and the one typed. */
export type DraftTimes = Record<AttendanceEvent, { old: string; value: string }>;

/** The times the form will send: typed, and not the same as the one recorded. */
export function changedEvents(times: DraftTimes): AttendanceEvent[] {
  return attendanceEvents.filter((type) => times[type].value.trim() !== times[type].old);
}

const shiftStart: Record<Shift, number> = { Day: 7 * 60, Night: 19 * 60 };

/** Minutes from 6 hours before the shift starts, so a Night shift's times after midnight still come after 19:00. */
function inShift(time: string, shift: Shift): number {
  const [hours, minutes] = time.split(':').map(Number);
  return (hours * 60 + minutes - shiftStart[shift] + 6 * 60 + 24 * 60) % (24 * 60);
}

/**
 * What is wrong with each time typed, checked before sending: the form (HH:mm), a recorded time left empty (it cannot
 * be deleted), and the order เข้างาน → ออกพัก → กลับจากพัก → เลิกงาน, said on the time that was changed. The server
 * checks the window and the future, and its message is shown as is.
 */
export function draftProblems(times: DraftTimes, shift: Shift): Partial<Record<AttendanceEvent, string>> {
  const problems: Partial<Record<AttendanceEvent, string>> = {};
  const changed = new Set(changedEvents(times));
  for (const type of changed) {
    const value = times[type].value.trim();
    if (value === '') {
      problems[type] = 'ลบเวลาไม่ได้ ใส่เวลาใหม่ หรือกดคืนค่า';
    } else if (!isClock(value)) {
      problems[type] = 'เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05';
    }
  }
  let before: { type: AttendanceEvent; time: string } | null = null;
  for (const type of attendanceEvents) {
    const time = changed.has(type) && !problems[type] ? times[type].value.trim() : times[type].old;
    if (time === '' || !isClock(time)) {
      continue;
    }
    if (before && inShift(time, shift) <= inShift(before.time, shift)) {
      if (changed.has(type)) {
        problems[type] ??= `ต้องหลังเวลา${eventLabel[before.type]} (${before.time})`;
      } else if (changed.has(before.type)) {
        problems[before.type] ??= `ต้องก่อนเวลา${eventLabel[type]} (${time})`;
      }
    }
    before = { type, time };
  }
  return problems;
}

/** The event the form opens on: the one asked for, else the first one the Cleaner has not scanned, else Shift-In. */
export function firstEvent(row: BoardRow, asked?: AttendanceEvent): AttendanceEvent {
  return asked ?? attendanceEvents.find((type) => !eventOf(row, type)) ?? 'ShiftIn';
}

/** From an alert's "เติมเวลา…" link (plan 11 decision 9): ?fill=<Cleaner's account id>&event=<AttendanceEvent>. */
export function readFill(params: URLSearchParams): { userId: number; event?: AttendanceEvent } | undefined {
  const userId = Number(params.get('fill'));
  if (!Number.isInteger(userId) || userId <= 0) {
    return undefined;
  }
  const event = params.get('event');
  return { userId, event: attendanceEvents.find((type) => type === event) };
}
