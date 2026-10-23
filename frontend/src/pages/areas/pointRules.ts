import type { Shift } from '../../shared/api/types';

/** One Round Window as the form holds it and the server takes it: Thai wall-clock "HH:mm" (facility-0047). */
export interface WindowDraft {
  shift: Shift;
  start: string;
  end: string;
}

/**
 * facility-0083 (amended 2026-10-23): a time typed as four digits in one field, as the server's two-digit 24-hour
 * "HH:mm" (RoundWindowRules.TryParseTime). The hour is 0–23 and the minute 00–59; anything else is null.
 */
export function clockOf(digits: string): string | null {
  if (!/^\d{4}$/.test(digits)) {
    return null;
  }
  const hour = Number(digits.slice(0, 2));
  const minute = Number(digits.slice(2));
  if (hour > 23 || minute > 59) {
    return null;
  }
  return `${digits.slice(0, 2)}:${digits.slice(2)}`;
}

/** The four digits as the field shows them while they are typed: "07" stays "07", "0700" reads "07:00". */
export function typedTime(digits: string): string {
  return digits.length > 2 ? `${digits.slice(0, 2)}:${digits.slice(2)}` : digits;
}

/** Minutes from the shift's start (07:00 or 19:00), wrapping past midnight: RoundWindowRules.MinutesIntoShift. */
export function minutesIntoShift(shift: Shift, clock: string): number {
  const [hours, minutes] = clock.split(':').map(Number);
  const shiftStart = shift === 'Day' ? 7 * 60 : 19 * 60;
  return (hours * 60 + minutes - shiftStart + 24 * 60) % (24 * 60);
}

/** Day before Night, then the order the shift meets them (a night 03:00 after 20:00), as the server lists them. */
export function sortWindows(windows: readonly WindowDraft[]): WindowDraft[] {
  return [...windows].sort((a, b) =>
    a.shift === b.shift
      ? minutesIntoShift(a.shift, a.start) - minutesIntoShift(b.shift, b.start)
      : a.shift === 'Day'
        ? -1
        : 1,
  );
}

export function sameWindow(a: WindowDraft, b: WindowDraft): boolean {
  return a.shift === b.shift && a.start === b.start && a.end === b.end;
}

/** The id of the point editor's heading; the Drawer that holds it takes its name from the same heading. */
export function pointEditorTitleId(pointId: number | undefined): string {
  return pointId === undefined ? 'point-editor-new' : `point-editor-${pointId}`;
}
