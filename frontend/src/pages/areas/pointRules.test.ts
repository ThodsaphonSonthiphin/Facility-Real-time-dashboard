import { describe, expect, it } from 'vitest';
import { clockOf, sortWindows, typedTime } from './pointRules';

describe('point rules (facility-0047)', () => {
  it('clockOf reads four typed digits as the server\'s "HH:mm" (facility-0083, amended 2026-10-23)', () => {
    expect(clockOf('0700')).toBe('07:00');
    expect(clockOf('0730')).toBe('07:30');
    expect(clockOf('2359')).toBe('23:59');
    expect(clockOf('0005')).toBe('00:05');
    expect(clockOf('2400')).toBeNull();
    expect(clockOf('0760')).toBeNull();
    expect(clockOf('700')).toBeNull();
    expect(clockOf('')).toBeNull();
    expect(clockOf('07:00')).toBeNull();
    expect(clockOf('07a0')).toBeNull();
  });

  it('typedTime shows the digits as they are typed, the colon after the second', () => {
    expect(typedTime('')).toBe('');
    expect(typedTime('0')).toBe('0');
    expect(typedTime('07')).toBe('07');
    expect(typedTime('070')).toBe('07:0');
    expect(typedTime('0700')).toBe('07:00');
  });

  it('orders windows Day first, then by time into the shift (a night 03:00 after 20:00)', () => {
    expect(
      sortWindows([
        { shift: 'Night', start: '03:00', end: '05:00' },
        { shift: 'Day', start: '16:00', end: '18:00' },
        { shift: 'Night', start: '20:00', end: '22:00' },
        { shift: 'Day', start: '07:00', end: '09:00' },
      ]),
    ).toEqual([
      { shift: 'Day', start: '07:00', end: '09:00' },
      { shift: 'Day', start: '16:00', end: '18:00' },
      { shift: 'Night', start: '20:00', end: '22:00' },
      { shift: 'Night', start: '03:00', end: '05:00' },
    ]);
  });
});
