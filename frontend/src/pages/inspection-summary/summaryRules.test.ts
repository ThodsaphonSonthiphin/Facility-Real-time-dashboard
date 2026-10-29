import { describe, expect, it } from 'vitest';
import { summaryRow } from '../../test/reportFixtures';
import { inspectedShare, placeText, pointsText } from './summaryRules';

describe('summaryRules (D7)', () => {
  it('pointsText says how many of the beat were inspected, and how many were not', () => {
    expect(pointsText(summaryRow())).toBe('12 จาก 12 จุด');
    expect(pointsText(summaryRow({ pointsInspected: 9 }))).toBe('9 จาก 12 จุด · ไม่ได้ตรวจ 3 จุด');
  });

  it('inspectedShare is the rounded-lg share of submitted work inspected, none when nothing was submitted', () => {
    expect(inspectedShare(summaryRow())).toBe(57);
    expect(inspectedShare(summaryRow({ submitted: 150, submittedInspected: 40 }))).toBe(27);
    expect(inspectedShare(summaryRow({ submitted: 0, submittedInspected: 0 }))).toBeNull();
  });

  it('placeText is the building and the shift', () => {
    expect(placeText(summaryRow())).toBe('A · กะเช้า');
    expect(placeText(summaryRow({ buildingCode: 'B', shift: 'Night' }))).toBe('B · กะดึก');
  });

  it('a known building shows its current name (facility-0091)', () => {
    expect(placeText(summaryRow(), new Map([['A', 'ตึกทดลอง']]))).toBe('A · ตึกทดลอง · กะเช้า');
  });
});
