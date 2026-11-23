import { describe, expect, it } from 'vitest';
import {
  availabilityLabel,
  cellLabel,
  clockSlot,
  initialScheduleSelection,
  initialScheduleWeek,
  parseScheduleWeek,
  readScheduleWeek,
  resolveScheduleWeek,
  selectionAfterRead,
  sourceLabel,
  weekDays,
  weekIsUnavailable,
  weekRangeLabel,
  weekStartOf,
} from './scheduleRules';
import type { ScheduleDay } from '../../shared/api/scheduleTypes';

/**
 * ADR facility-0180–0186 and 0218: which week a schedule opens on, which week it returns to, and which filters survive a
 * return. The rules are pure, so every case here is decided without a server, a browser or a clock the test cannot set.
 */
describe('schedule week selection (facility-0180, 0181, 0182, 0183)', () => {
  it('restores a deliberately selected week before applying initial defaults', () => {
    expect(resolveScheduleWeek('2026-11-23', '2026-11-16')).toBe('2026-11-23');
  });

  it('opens this week when nothing was viewed yet', () => {
    expect(resolveScheduleWeek(undefined, '2026-11-16')).toBe('2026-11-16');
  });

  it('a week starts on Monday, whatever weekday the date is', () => {
    expect(weekStartOf('2026-11-16')).toBe('2026-11-16');
    expect(weekStartOf('2026-11-22')).toBe('2026-11-16'); // a Sunday belongs to the week that began on the 16th
    expect(weekStartOf('2026-11-23')).toBe('2026-11-23');
  });

  it('reads a foreign or impossible date as no week at all, instead of passing it to the server', () => {
    expect(readScheduleWeek('23/11/2026')).toBeUndefined();
    expect(readScheduleWeek('2026-02-30')).toBeUndefined();
    expect(readScheduleWeek('2026-1-5')).toBeUndefined();
    expect(readScheduleWeek('')).toBeUndefined();
    expect(readScheduleWeek(undefined)).toBeUndefined();
    // A real date is normalised to its Monday, so a mid-week value cannot ask for half a week
    expect(readScheduleWeek('2026-11-25')).toBe('2026-11-23');
  });

  it('falls back to the initial week when a restored or typed value is not an ISO date', () => {
    expect(parseScheduleWeek('23/11/2026', '2026-11-16')).toBe('2026-11-16');
    expect(parseScheduleWeek(null, '2026-11-16')).toBe('2026-11-16');
    expect(parseScheduleWeek('2026-11-23', '2026-11-16')).toBe('2026-11-23');
  });

  it('names the seven dates of a week from its Monday', () => {
    expect(weekDays('2026-11-16')).toEqual([
      '2026-11-16',
      '2026-11-17',
      '2026-11-18',
      '2026-11-19',
      '2026-11-20',
      '2026-11-21',
      '2026-11-22',
    ]);
  });
});

describe('the shift on the clock (facility-0040, 0183)', () => {
  it('reads a Thai morning as the day shift that started today', () => {
    // 02:00Z is 09:00 in Thailand
    expect(clockSlot(new Date('2026-11-17T02:00:00Z'))).toEqual({
      today: '2026-11-17',
      shift: 'Day',
      startDate: '2026-11-17',
    });
  });

  it('reads 19:00 Thai as the night shift that started that same date', () => {
    expect(clockSlot(new Date('2026-11-17T13:00:00Z'))).toEqual({
      today: '2026-11-17',
      shift: 'Night',
      startDate: '2026-11-17',
    });
  });

  it('reads Monday 02:00 Thai as Sunday night still running', () => {
    // Monday 23/11 02:00 Thai is 22/11 19:00Z: the night shift that started on Sunday the 22nd
    expect(clockSlot(new Date('2026-11-22T19:00:00Z'))).toEqual({
      today: '2026-11-23',
      shift: 'Night',
      startDate: '2026-11-22',
    });
  });
});

describe('the week a schedule first opens on (facility-0183, 0184)', () => {
  it('uses the week of today for a day shift', () => {
    expect(initialScheduleWeek('2026-11-17', 'Day', '2026-11-17')).toBe('2026-11-16');
  });

  it('keeps the start week of an ongoing Sunday-to-Monday night shift', () => {
    // The clock says Monday the 23rd; the shift being worked began on Sunday the 22nd, so 16–22 November stays on screen
    expect(initialScheduleWeek('2026-11-23', 'Night', '2026-11-22')).toBe('2026-11-16');
  });

  it('uses the week of today for a night shift that began today', () => {
    expect(initialScheduleWeek('2026-11-23', 'Night', '2026-11-23')).toBe('2026-11-23');
  });
});

describe('the initial filters of each role (facility-0184, 0186)', () => {
  const dayClock = { today: '2026-11-17', shift: 'Day' as const, startDate: '2026-11-17' };

  it('opens a Cleaner on this week with no filter: the read is only ever their own row', () => {
    expect(initialScheduleSelection('cleaner', dayClock)).toEqual({ weekStart: '2026-11-16' });
  });

  it('opens a Supervisor on every assigned building in the own shift, which the server scope already answers', () => {
    // No building chosen is "all of them"; no shift is sent, so the read stays inside the Supervisor's own shift
    expect(initialScheduleSelection('supervisor', dayClock)).toEqual({ weekStart: '2026-11-16' });
  });

  it('opens an Admin on every building in the shift on the clock (facility-0184)', () => {
    expect(initialScheduleSelection('admin', dayClock)).toEqual({ weekStart: '2026-11-16', shift: 'Day' });
  });

  it('opens an Admin on the clock shift, with the night shift keeping its own start week', () => {
    const sundayNight = { today: '2026-11-23', shift: 'Night' as const, startDate: '2026-11-22' };
    expect(initialScheduleSelection('admin', sundayNight)).toEqual({ weekStart: '2026-11-16', shift: 'Night' });
  });
});

describe('a restored filter the actor no longer holds (facility-0185)', () => {
  const week = { weekStart: '2026-11-16', buildingId: 2, shift: 'Night' as const };
  const readable = { availability: 'available' as const, rowCount: 3 };
  const nothingReadable = { availability: 'empty' as const, rowCount: 0 };

  it('keeps a restored filter the server can still answer for, together with the week and the shift', () => {
    expect(selectionAfterRead(week, readable)).toEqual({ selection: week, dropped: false });
  });

  it('drops a restored filter the server has nothing readable for, keeping the week and the shift', () => {
    expect(selectionAfterRead(week, nothingReadable)).toEqual({
      selection: { weekStart: '2026-11-16', shift: 'Night' },
      dropped: true,
    });
  });

  it('drops a restored filter the server answers as unavailable, because that is not a plan either', () => {
    expect(selectionAfterRead(week, { availability: 'unavailable', rowCount: 0 })).toEqual({
      selection: { weekStart: '2026-11-16', shift: 'Night' },
      dropped: true,
    });
  });

  it('has nothing to drop when every building was chosen', () => {
    expect(selectionAfterRead({ weekStart: '2026-11-16' }, nothingReadable)).toEqual({
      selection: { weekStart: '2026-11-16' },
      dropped: false,
    });
  });
});

describe('the words a schedule shows (spec §5, §10)', () => {
  it('names each availability word in Thai, so none is shown as a colour or an empty cell', () => {
    expect(availabilityLabel).toEqual({
      available: 'มีแผนทำงาน',
      'day-off': 'วันหยุด',
      leave: 'ลา',
      unset: 'ยังไม่กำหนด',
      unavailable: 'ไม่มีข้อมูลของวันนี้',
    });
  });

  it('names each source kind, so a dated choice is never read as a recurring plan', () => {
    expect(sourceLabel).toEqual({
      dated: 'กำหนดเฉพาะวัน',
      baseline: 'ตามแผนประจำ',
      unset: 'ยังไม่มีแผน',
      unavailable: 'ไม่มีข้อมูล',
    });
  });

  it('gives a table cell a short word that still says which state it is', () => {
    expect(cellLabel).toEqual({
      available: 'ทำงาน',
      'day-off': 'หยุด',
      leave: 'ลา',
      unset: 'ไม่กำหนด',
      unavailable: 'ไม่มีข้อมูล',
    });
  });
});

/** One resolved day, for the two whole-week questions below. */
function day(availability: ScheduleDay['availability']): ScheduleDay {
  return {
    date: '2026-11-16',
    shift: 'Day',
    area: { value: 1, sourceId: 5, sourceKind: 'baseline', revision: '3' },
    breakGroup: { value: 1, sourceId: 5, sourceKind: 'baseline', revision: '3' },
    isDayOff: availability === 'day-off',
    isLeave: availability === 'leave',
    isUnset: availability === 'unset',
    availability,
    problems: [],
  };
}

describe('unavailable is not the same as empty (spec §3, §10)', () => {
  it('tells a week that is outside retained coverage from a week with nothing to read', () => {
    // A27: the dates exist but no paperwork survives them — never drawn as "nobody is planned"
    expect(weekIsUnavailable({ availability: 'unavailable', rows: [{ days: [day('unavailable')] }] })).toBe(true);
    // `empty`: the actor may read nothing here at all; the roster is genuinely without people, not without history
    expect(weekIsUnavailable({ availability: 'empty', rows: [] })).toBe(false);
    // A real plan, even one whose single day is unknown, is not "unavailable"
    expect(weekIsUnavailable({ availability: 'available', rows: [{ days: [day('unset')] }] })).toBe(false);
  });

  it('keeps `unavailable` distinct from a day nobody has chosen yet', () => {
    expect(availabilityLabel.unavailable).not.toBe(availabilityLabel.unset);
    expect(weekIsUnavailable({ availability: 'available', rows: [{ days: [day('unset')] }] })).toBe(false);
  });
});

describe('the week as a person reads it', () => {
  it('writes the range once in Thai Buddhist-year format inside one month', () => {
    expect(weekRangeLabel('2026-11-16')).toBe('16 – 22 พ.ย. 2569');
  });

  it('writes both months when the week crosses one', () => {
    expect(weekRangeLabel('2026-11-30')).toBe('30 พ.ย. – 6 ธ.ค. 2569');
  });

  it('writes both years when the week crosses New Year', () => {
    expect(weekRangeLabel('2026-12-28')).toBe('28 ธ.ค. 2569 – 3 ม.ค. 2570');
  });
});
