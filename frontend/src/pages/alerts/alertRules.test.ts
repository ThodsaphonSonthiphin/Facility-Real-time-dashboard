import { describe, expect, it } from 'vitest';
import {
  alerts,
  attendanceGpsAlert,
  blockedAlert,
  inspectionGpsAlert,
  missingAlert,
  reviewedAlert,
} from '../../test/liveFixtures';
import { alertDetail, alertTitle, alertWho, fillLink, filterAlerts, slotOfShiftEnd } from './alertRules';

describe('alert rules (wireframe D3)', () => {
  it('splits by talked-about or not, and puts the three GPS flags under one type', () => {
    expect(filterAlerts(alerts, 'open', 'all')).toHaveLength(4);
    expect(filterAlerts(alerts, 'reviewed', 'all')).toEqual([reviewedAlert]);
    expect(filterAlerts(alerts, 'all', 'far')).toEqual([inspectionGpsAlert, attendanceGpsAlert, reviewedAlert]);
    expect(filterAlerts(alerts, 'open', 'blocked')).toEqual([blockedAlert]);
    expect(filterAlerts(alerts, 'open', 'missing')).toEqual([missingAlert]);
  });

  it("names a Blocked Scan by why it was refused, never the old 'สแกนนอก Area' for a Supervisor", () => {
    expect(alertTitle(blockedAlert)).toBe('สแกนนอก Area (ถูกบล็อก)');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OtherBuilding' })).toBe('สแกนนอกตึก (ถูกบล็อก)');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OutsideShift' })).toBe('สแกนนอกกะ (ถูกบล็อก)');
    expect(alertTitle({ ...missingAlert, missingEvents: ['BreakIn', 'ShiftOut'] })).toBe('ไม่ได้ลงเวลากลับจากพัก และเลิกงาน');
    expect(alertWho(inspectionGpsAlert)).toBe('หัวหน้าทดสอบ (หัวหน้า)');
  });

  it('says where it happened, as far as the server knows', () => {
    expect(alertDetail(blockedAlert)).toBe(
      'ป้าย AR12-02 ห้องน้ำหญิง ของ AR12 Area สิบสอง · Area ของแม่บ้านที่อื่นคือ AR03 · ไม่ได้บันทึกงาน',
    );
    expect(alertDetail(attendanceGpsAlert)).toBe(
      'ป้ายลงเวลา AR01-IN · AR01 Area ทดลอง · ห่าง 85 ม. (รัศมี 50 ม.) · ความแม่นยำ ±40 ม.',
    );
    expect(alertDetail({ ...attendanceGpsAlert, accuracyM: undefined, radiusM: undefined })).toBe(
      'ป้ายลงเวลา AR01-IN · AR01 Area ทดลอง · ห่าง 85 ม.',
    );
    expect(alertDetail(missingAlert)).toBe('กะดึก 14/10/2026 · AR01 Area ทดลอง');
  });

  it("works out a missing-attendance alert's shift from its time, the shift's end (facility-0040)", () => {
    expect(slotOfShiftEnd('2026-10-15T12:00:00Z')).toEqual({ shiftDate: '2026-10-15', shift: 'Day' }); // 19:00
    expect(slotOfShiftEnd('2026-10-15T00:00:00Z')).toEqual({ shiftDate: '2026-10-14', shift: 'Night' }); // 07:00
    expect(slotOfShiftEnd('2026-11-01T00:00:00Z')).toEqual({ shiftDate: '2026-10-31', shift: 'Night' });
    expect(fillLink(missingAlert)).toBe('/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut');
  });
});
