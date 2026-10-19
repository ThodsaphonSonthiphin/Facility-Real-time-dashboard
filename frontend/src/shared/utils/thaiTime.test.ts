import { describe, expect, it } from 'vitest';
import { formatDate, formatMinutes, formatRound, formatShiftDate, formatTime, parseShiftDate } from './thaiTime';

describe('thaiTime', () => {
  it('formatShiftDate writes a shift date from the server (already Thai) as day/month/year', () => {
    expect(formatShiftDate('2026-10-05')).toBe('05/10/2026');
  });

  it('parseShiftDate reads a typed day/month/year, and refuses what is not a real date in that form', () => {
    expect(parseShiftDate('15/10/2026')).toBe('2026-10-15');
    expect(parseShiftDate(' 01/01/2027 ')).toBe('2027-01-01');
    expect(parseShiftDate('31/02/2026')).toBeNull();
    expect(parseShiftDate('5/10/2026')).toBeNull();
    expect(parseShiftDate('2026-10-15')).toBeNull();
    expect(parseShiftDate('')).toBeNull();
  });

  it('formatDate gives the Thai calendar date, which may be a day after the UTC date', () => {
    expect(formatDate('2026-10-15T18:30:00Z')).toBe('16/10/2026'); // 01:30 on the 16th in Thailand
    expect(formatDate('2026-10-16T02:00:00Z')).toBe('16/10/2026');
  });

  it('shows a UTC moment from the server as Thai time on a 24-hour clock', () => {
    expect(formatTime('2026-10-14T23:52:00Z')).toBe('06:52');
    expect(formatTime('2026-10-15T17:05:00Z')).toBe('00:05');
  });

  it('shows a Date from the phone as Thai time', () => {
    expect(formatTime(new Date('2026-10-15T03:04:00Z'))).toBe('10:04');
  });

  it('drops the seconds of a round window', () => {
    expect(formatRound({ start: '07:00:00', end: '09:00:00' })).toBe('07:00–09:00');
  });

  it('says minutes as a Thai duration', () => {
    expect(formatMinutes(25)).toBe('25 นาที');
    expect(formatMinutes(85)).toBe('1 ชม. 25 นาที');
    expect(formatMinutes(120)).toBe('2 ชม.');
  });
});
