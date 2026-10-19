import { describe, expect, it } from 'vitest';
import { isClock, sortWindows } from './pointRules';

describe('point rules (facility-0047)', () => {
  it('accepts only a 24-hour "HH:mm", as the server does', () => {
    expect(isClock('07:00')).toBe(true);
    expect(isClock('23:59')).toBe(true);
    expect(isClock('7:00')).toBe(false);
    expect(isClock('24:00')).toBe(false);
    expect(isClock('07:60')).toBe(false);
    expect(isClock('0700')).toBe(false);
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
