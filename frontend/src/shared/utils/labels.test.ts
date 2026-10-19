import { describe, expect, it } from 'vitest';
import { formatCoordinates, roundsText, signPlaceLabel } from './labels';

describe('Admin setup labels', () => {
  it('names where a sign location came from (facility-0045)', () => {
    expect(signPlaceLabel({ locationSource: 'Site', locationAccuracyM: 14 })).toBe('หน้างาน ±14 ม.');
    expect(signPlaceLabel({ locationSource: 'Map' })).toBe('ยังไม่ยืนยันหน้างาน (จากแผนที่)');
    expect(signPlaceLabel({})).toBe('ยังไม่มีพิกัด');
  });

  it('lists a point’s rounds by shift, in the order the server sends them', () => {
    expect(
      roundsText([
        { shift: 'Day', start: '07:00', end: '09:00' },
        { shift: 'Day', start: '16:00', end: '18:00' },
        { shift: 'Night', start: '20:00', end: '22:00' },
        { shift: 'Night', start: '03:00', end: '05:00' },
      ]),
    ).toBe('เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00');
    expect(roundsText([{ shift: 'Night', start: '20:00', end: '22:00' }])).toBe('ดึก 20:00–22:00');
    expect(roundsText([])).toBe('ยังไม่มีรอบ');
  });

  it('writes coordinates with 6 decimals, as the server stores them', () => {
    expect(formatCoordinates(13.7563, 100.5018)).toBe('13.756300, 100.501800');
  });
});
