import { describe, expect, it } from 'vitest';
import { formatMinutes, formatRound, formatTime } from './thaiTime';

describe('thaiTime', () => {
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
