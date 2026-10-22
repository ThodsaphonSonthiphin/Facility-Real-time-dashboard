import { describe, expect, it } from 'vitest';
import { clockOf, sortWindows } from './pointRules';

describe('point rules (facility-0047)', () => {
  it('clockOf joins an hour box and a minute box as the server\'s "HH:mm" (facility-0083)', () => {
    expect(clockOf('7', '00')).toBe('07:00');
    expect(clockOf('07', '30')).toBe('07:30');
    expect(clockOf('10', '')).toBe('10:00');
    expect(clockOf(' 23 ', ' 59 ')).toBe('23:59');
    expect(clockOf('0', '05')).toBe('00:05');
    expect(clockOf('24', '00')).toBeNull();
    expect(clockOf('', '00')).toBeNull();
    expect(clockOf('07', '5')).toBeNull();
    expect(clockOf('07', '60')).toBeNull();
    expect(clockOf('0700', '')).toBeNull();
    expect(clockOf('7a', '00')).toBeNull();
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
