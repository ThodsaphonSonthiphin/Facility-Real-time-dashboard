import type { Shift } from '../../shared/api/types';

/** One Round Window as the form holds it and the server takes it: Thai wall-clock "HH:mm" (facility-0047). */
export interface WindowDraft {
  shift: Shift;
  start: string;
  end: string;
}

/**
 * facility-0083: a time typed as an hour box and a minute box, as the server's two-digit 24-hour "HH:mm"
 * (RoundWindowRules.TryParseTime). The hour is 0–23 in one or two digits; the minute is empty (00) or two digits
 * 00–59. Anything else is null.
 */
export function clockOf(hour: string, minute: string): string | null {
  const h = hour.trim();
  const m = minute.trim() === '' ? '00' : minute.trim();
  if (!/^\d{1,2}$/.test(h) || !/^\d{2}$/.test(m) || Number(h) > 23 || Number(m) > 59) {
    return null;
  }
  return `${h.padStart(2, '0')}:${m}`;
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
