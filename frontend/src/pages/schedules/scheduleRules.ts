import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import type { Shift } from '../../shared/api/types';
import type { ScheduleAvailability, SchedulePerson, ScheduleSourceKind, ScheduleWeekAvailability } from '../../shared/api/scheduleTypes';

/**
 * The week and filter rules of the scheduling pages, pure so every case is decidable without a server or a clock the test
 * cannot set (ADR facility-0180–0186, 0218):
 *
 * - first opening shows this week (0180, 0182), except that an ongoing night shift keeps its start week (0183);
 * - returning during the same ongoing use restores the week and the applicable filters (0181, 0182, 0185);
 * - reload, a new tab and a new login start from the initial defaults (0218) — the memory is in-memory only;
 * - a restored filter is a selection, not an authority (0185): one the actor no longer holds is dropped at read time.
 *
 * An ISO week is a Monday. Every week value that enters this module is normalised to its Monday and every value that is
 * not a real ISO date is refused at the parsing boundary, so a foreign date never reaches the server.
 */

export const DAY_MS = 86_400_000;

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** A real "yyyy-MM-dd" date as a UTC instant, or null when the text is not one (2026-02-30 included). */
function utcDate(text: string): number | null {
  if (!ISO_DATE.test(text)) {
    return null;
  }
  const at = Date.parse(`${text}T00:00:00Z`);
  return Number.isNaN(at) || new Date(at).toISOString().slice(0, 10) !== text ? null : at;
}

/** The Monday of the week that holds `date`, or null when `date` is not a real ISO date. */
export function weekStartOf(date: string): string | null {
  const at = utcDate(date);
  if (at === null) {
    return null;
  }
  // 1970-01-01 was a Thursday, so (day + 3) mod 7 is the offset back to Monday
  const weekday = new Date(at).getUTCDay();
  return addDays(date, -((weekday + 6) % 7));
}

/**
 * A week value read from a restored selection or a query parameter, as the Monday it belongs to; undefined when it is not
 * a real ISO date. Validation belongs here, at the boundary: a value that fails is never sent to the server.
 */
export function readScheduleWeek(value: string | null | undefined): string | undefined {
  return value ? (weekStartOf(value) ?? undefined) : undefined;
}

/** The week to show, with a foreign restored or typed value falling back to the initial week. */
export function parseScheduleWeek(value: string | null | undefined, initial: string): string {
  return readScheduleWeek(value) ?? initial;
}

/**
 * The restoration rule (0181, 0218): a week the person deliberately chose during this ongoing use wins; nothing chosen yet
 * means the initial week. It does not validate — `readScheduleWeek`/`parseScheduleWeek` do that before this is asked.
 */
export function resolveScheduleWeek(restored: string | undefined, initial: string): string {
  return restored ?? initial;
}

/** The seven dates of a week, Monday first. */
export function weekDays(weekStart: string): string[] {
  return Array.from({ length: 7 }, (_, offset) => addDays(weekStart, offset));
}

/** The shift on the clock (facility-0040): Day 07:00–19:00, Night 19:00–07:00 Thai, and a night keeps its start date. */
export interface ClockSlot {
  /** Today's Thai calendar date, as the server writes a shift date. */
  today: string;
  shift: Shift;
  /** The date the shift on the clock started on: the previous date for a night shift after midnight (0183). */
  startDate: string;
}

/**
 * The shift on the clock, from the moment given. It mirrors the server's `ShiftCalendar.SlotAt` (Thai 07:00/19:00
 * boundaries) so the Admin's initial shift is the one the server agrees is current. The moment is a parameter, so a test
 * decides it instead of hoping for the machine's clock.
 */
export function clockSlot(now: Date): ClockSlot {
  const thai = new Date(now.getTime() + 7 * 60 * 60_000); // Thai time is UTC+7 all year (no daylight saving)
  const today = thai.toISOString().slice(0, 10);
  const minutes = thai.getUTCHours() * 60 + thai.getUTCMinutes();
  if (minutes < 7 * 60) {
    // Before 07:00 Thai the previous night is still running, so it started on the date before
    return { today, shift: 'Night', startDate: addDays(today, -1) };
  }
  // 07:00–19:00 is the day shift, 19:00 onwards the night that starts today
  return minutes < 19 * 60 ? { today, shift: 'Day', startDate: today } : { today, shift: 'Night', startDate: today };
}

/**
 * The week a schedule first opens on (0180, 0182, 0183, 0184): the week containing the clock slot's START date, so an
 * ongoing Sunday-to-Monday night shift keeps the week it began in. `today` is unused by the rule and kept in the signature
 * because the ADRs are stated in terms of "the clock's date"; the start date is what decides.
 */
export function initialScheduleWeek(_today: string, _shift: Shift, clockDateStart: string): string {
  return weekStartOf(clockDateStart) ?? clockDateStart;
}

/** The role whose schedule is open; a Cleaner's read is only ever their own row, so it carries no filter at all. */
export type ScheduleRole = 'cleaner' | 'supervisor' | 'admin';

/** What one role's view holds: the week, and for a management view the building and shift filters. */
export interface ScheduleSelection {
  weekStart: string;
  /** Missing is every building the actor may read (0186 for a Supervisor, 0184 for an Admin). */
  buildingId?: number;
  /** Missing is the actor's own shift; only an Admin sends one, since a Supervisor's read stays inside their own shift. */
  shift?: Shift;
}

/**
 * The filters a schedule first opens with (0180–0186). A Cleaner has none. A Supervisor opens on every building assigned
 * to them in their own shift, which is exactly the read with no building and no shift. An Admin opens on every building
 * in the shift on the clock.
 */
export function initialScheduleSelection(role: ScheduleRole, clock: ClockSlot): ScheduleSelection {
  const weekStart = initialScheduleWeek(clock.today, clock.shift, clock.startDate);
  if (role === 'admin') {
    return { weekStart, shift: clock.shift };
  }
  return { weekStart };
}

/** What a restored selection becomes once the server has answered for it. */
export interface HeldSelection {
  selection: ScheduleSelection;
  /** True when the restored building was dropped, so the page can say the filter changed rather than pretend it chose. */
  dropped: boolean;
}

/**
 * 0185 restores a selection, never an authority: a building the actor no longer holds answers with nothing readable, and
 * the server tells that apart from "a building with no plan" by answering the week `empty` (`ScheduleStore.AvailabilityEmpty`
 * — "nothing the actor may read"). Such a filter is dropped at read time and the view falls back to every building the
 * actor may read, which is the smallest honest behaviour: keeping it would present a forbidden building as if it were
 * chosen, and keeping the empty screen would read as "nobody is planned". A week that does have readable rows keeps the
 * filter. Nothing here can loop: the fallback itself carries no building.
 */
export function selectionAfterRead(
  selection: ScheduleSelection,
  week: { availability: ScheduleWeekAvailability; rowCount: number },
): HeldSelection {
  if (selection.buildingId === undefined) {
    return { selection, dropped: false };
  }
  const readable = week.availability !== 'empty' && week.rowCount > 0;
  if (readable) {
    return { selection, dropped: false };
  }
  return { selection: { weekStart: selection.weekStart, shift: selection.shift }, dropped: true };
}

/** The words for one resolved day (spec §5, §10). Every state says its words; none is shown as a colour alone. */
export const availabilityLabel: Record<ScheduleAvailability, string> = {
  available: 'มีแผนทำงาน',
  'day-off': 'วันหยุด',
  leave: 'ลา',
  unset: 'ยังไม่กำหนด',
  unavailable: 'ไม่มีข้อมูลของวันนี้',
};

/** The same states as a table cell's short word; short, but it still names the state rather than a colour. */
export const cellLabel: Record<ScheduleAvailability, string> = {
  available: 'ทำงาน',
  'day-off': 'หยุด',
  leave: 'ลา',
  unset: 'ไม่กำหนด',
  unavailable: 'ไม่มีข้อมูล',
};

/** Where a value came from, so a one-day choice is never read as a recurring plan (spec §5). */
export const sourceLabel: Record<ScheduleSourceKind, string> = {
  dated: 'กำหนดเฉพาะวัน',
  baseline: 'ตามแผนประจำ',
  unset: 'ยังไม่มีแผน',
  unavailable: 'ไม่มีข้อมูล',
};

/** Thai display labels for a cleaner's schedule kind. */
export const cleanerKindLabel: Record<SchedulePerson['cleanerKind'], string> = {
  Regular: 'แม่บ้านประจำ',
  Relief: 'แม่บ้านสแปร์',
  unknown: 'ยังไม่ระบุ',
};

/**
 * True when the whole week carries no readable day at all (A27's `unavailable`). It is the one case the page must never
 * draw as an empty roster: the dates are outside retained coverage, so "fewer rows" would be a false statement about the
 * plan (spec §3, §10).
 */
export function weekIsUnavailable(week: ScheduleWeekLike): boolean {
  return (
    week.availability === 'unavailable' &&
    week.rows.every((row) => row.days.every((day) => day.availability === 'unavailable'))
  );
}

interface ScheduleWeekLike {
  availability: ScheduleWeekAvailability;
  rows: readonly { days: readonly { availability: ScheduleAvailability }[] }[];
}

/** What RTK Query tells us about the read on screen: when the latest one started and when the last one succeeded. */
export interface ReadTimes {
  /** Milliseconds since the epoch; undefined while nothing has been asked or on a refusal. */
  startedTimeStamp?: number;
  fulfilledTimeStamp?: number;
}

/**
 * Spec §12: whether what the page shows may be older than the stored plan. A pushed change marks the moment it arrived
 * (`changedAt`); the notice stays until a read that STARTED after that mark SUCCEEDS. A request beginning, a reconnect or
 * a failed read is never evidence that the screen is current, and a request that started before the push can land after it
 * carrying the old database state — which is why both times must be after the mark: the latest request began after it and
 * the answer on screen was received after it. A failed latest read leaves an older `fulfilledTimeStamp` and stays stale.
 */
export function readMayBeStale(changedAt: number | null, read: ReadTimes | undefined): boolean {
  if (changedAt == null) {
    return false;
  }
  return (
    read?.fulfilledTimeStamp == null ||
    (read.startedTimeStamp ?? 0) < changedAt ||
    read.fulfilledTimeStamp < changedAt
  );
}

/**
 * A week as a person reads it: "16–22 พ.ย. 2026" (or "30 พ.ย. – 6 ธ.ค. 2026" across a month). The server answers dates,
 * not names, so this is the only place the range is worded.
 */
export function weekRangeLabel(weekStart: string): string {
  const end = addDays(weekStart, 6);
  const startText = formatShiftDate(weekStart);
  const endText = formatShiftDate(end);
  // The year is written once, after the range: both ends always share it because a week never spans a year boundary
  return `${startText.slice(0, 5)}–${endText.slice(0, 5)} ${endText.slice(6)}`;
}
