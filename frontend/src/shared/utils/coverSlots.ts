import type { Shift } from '../api/types';
import { addDays } from './dateRange';
import { shiftShortLabel } from './labels';
import { formatShiftDate, formatWeekday } from './thaiTime';

/** A shift the cover box shows (facility-0075). */
export interface CoverSlot {
  shiftDate: string;
  shift: Shift;
}

/** facility-0075: covers are assigned up to 7 days ahead, so the night and the weekend never wait for an Admin. */
export const AHEAD_DAYS = 7;

/** From the shift on the clock to the Night shift 7 days later, in the order they come. */
export function coverSlots(current: CoverSlot): CoverSlot[] {
  const slots: CoverSlot[] = [];
  for (let day = 0; day <= AHEAD_DAYS; day += 1) {
    const shiftDate = addDays(current.shiftDate, day);
    for (const shift of ['Day', 'Night'] as const) {
      // On the first date a Night shift on the clock has already passed that date's Day shift
      if (day > 0 || current.shift === 'Day' || shift === 'Night') {
        slots.push({ shiftDate, shift });
      }
    }
  }
  return slots;
}

export function sameSlot(a: CoverSlot, b: CoverSlot): boolean {
  return a.shiftDate === b.shiftDate && a.shift === b.shift;
}

/** The picker's value, e.g. "2026-10-16:Night". */
export function slotKey(slot: CoverSlot): string {
  return `${slot.shiftDate}:${slot.shift}`;
}

/** "กะดึก ศ. 16/10/2026": the weekday matters, because the Admin thinks "Saturday", not a date. */
export function slotText(slot: CoverSlot): string {
  return `${shiftShortLabel[slot.shift]} ${formatWeekday(slot.shiftDate)} ${formatShiftDate(slot.shiftDate)}`;
}

/** When a shift's cover ends by itself: the Day shift at 19:00, the Night shift at 07:00 (facility-0040). */
export const shiftEndText: Record<Shift, string> = { Day: '19:00', Night: '07:00' };
