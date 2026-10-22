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

/** facility-0035: the scans recorded outside their sign's radius. No verdict (an Admin's time, a sign with no location) is no flag. */
export function flagText(row: BoardRow): string {
  return row.events
    .filter((event) => event.withinRadius === false)
    .map((event) => `${eventLabel[event.eventType]} ห่างป้าย ${event.distanceM ?? '?'} ม.`)
    .join(' · ');
}

/** A cover under the Area's Cleaner (wireframe D2): who, their own Area, when the Admin assigned them, and their Shift-In. */
export function coverLine(cover: BoardCover): string {
  return [
    `ทำแทน: ${cover.displayName}${cover.ownAreaCode ? ` (${cover.ownAreaCode})` : ''}`,
    `แอดมินมอบหมาย ${formatTime(cover.assignedAt)}`,
    cover.shiftInAt ? `เข้างาน ${formatTime(cover.shiftInAt)}` : 'ยังไม่ลงเวลา',
  ].join(' · ');
}

/** facility-0051: AdminAttendanceEndpoints.MaxReasonLength. */
export const MAX_REASON = 500;

/** Checked before sending; the server checks the window, the order and the future, and its message is shown as is. */
export function correctionProblem({ time, reason }: { time: string; reason: string }): string | null {
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time.trim())) {
    return 'เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05';
  }
  const text = reason.trim();
  if (text === '') {
    return 'ต้องกรอกเหตุผล';
  }
  return text.length > MAX_REASON ? `เหตุผลยาวได้ไม่เกิน ${MAX_REASON} ตัวอักษร` : null;
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
