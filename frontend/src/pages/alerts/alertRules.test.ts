import { describe, expect, it } from 'vitest';
import {
  alerts,
  attendanceGpsAlert,
  blockedAlert,
  inspectionGpsAlert,
  missingAlert,
  candidates,
  emptyRow,
  noCleanerRow,
  reviewedAlert,
} from '../../test/liveFixtures';
import {
  alertDetail,
  alertTitle,
  alertWho,
  candidateText,
  coverNotice,
  coverSlots,
  fillLink,
  filterAlerts,
  matchesSearch,
  slotOfShiftEnd,
  slotText,
} from './alertRules';

const made = { id: 72, userId: 21, personName: 'แม่บ้านทดสอบ', shiftDate: '2026-10-15', shift: 'Day' as const, assignedAt: '2026-10-15T03:20:00Z' };
const supervisorCover = { ...made, personName: 'หัวหน้าทดสอบ', buildingId: 2, buildingCode: 'B' };

describe('alert rules (wireframe D3)', () => {
  it('coverSlots runs from the shift on the clock to the Night shift 7 days later (facility-0075)', () => {
    const fromDay = coverSlots({ shiftDate: '2026-10-15', shift: 'Day' });
    expect(fromDay).toHaveLength(16);
    expect(fromDay.slice(0, 3)).toEqual([
      { shiftDate: '2026-10-15', shift: 'Day' },
      { shiftDate: '2026-10-15', shift: 'Night' },
      { shiftDate: '2026-10-16', shift: 'Day' },
    ]);
    expect(fromDay[fromDay.length - 1]).toEqual({ shiftDate: '2026-10-22', shift: 'Night' });

    const fromNight = coverSlots({ shiftDate: '2026-10-31', shift: 'Night' });
    expect(fromNight).toHaveLength(15);
    expect(fromNight.slice(0, 2)).toEqual([
      { shiftDate: '2026-10-31', shift: 'Night' },
      { shiftDate: '2026-11-01', shift: 'Day' },
    ]);
    expect(fromNight[fromNight.length - 1]).toEqual({ shiftDate: '2026-11-07', shift: 'Night' });
  });

  it('slotText names the shift, the weekday and the date', () => {
    expect(slotText({ shiftDate: '2026-10-17', shift: 'Night' })).toBe('กะดึก ส. 17/10/2026');
  });

  it('matchesSearch finds a row by its regular Cleaner, Area code or Area name, ignoring case and spaces (facility-0084)', () => {
    expect(matchesSearch(emptyRow, '  ลา ')).toBe(true);
    expect(matchesSearch(emptyRow, 'ar12')).toBe(true);
    expect(matchesSearch(emptyRow, 'สิบสอง')).toBe(true);
    expect(matchesSearch(emptyRow, 'ทดสอบ')).toBe(false);
    expect(matchesSearch(noCleanerRow, '')).toBe(true);
    expect(matchesSearch(noCleanerRow, 'แม่บ้าน')).toBe(false);
  });

  it('splits by talked-about or not, and puts the three GPS flags under one type', () => {
    expect(filterAlerts(alerts, 'open', 'all')).toHaveLength(4);
    expect(filterAlerts(alerts, 'reviewed', 'all')).toEqual([reviewedAlert]);
    expect(filterAlerts(alerts, 'all', 'far')).toEqual([inspectionGpsAlert, attendanceGpsAlert, reviewedAlert]);
    expect(filterAlerts(alerts, 'open', 'blocked')).toEqual([blockedAlert]);
    expect(filterAlerts(alerts, 'open', 'missing')).toEqual([missingAlert]);
  });

  it("names a Blocked Scan by why it was refused, never the old 'สแกนนอก Area' for a Supervisor", () => {
    expect(alertTitle(blockedAlert)).toBe('สแกนผิดพื้นที่');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OtherBuilding' })).toBe('สแกนผิดตึก');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OutsideShift' })).toBe('สแกนนอกกะ');
    expect(alertTitle({ ...missingAlert, missingEvents: ['BreakIn', 'ShiftOut'] })).toBe('ไม่ได้ลงเวลากลับจากพัก และเลิกงาน');
    expect(alertWho(inspectionGpsAlert)).toBe('หัวหน้าทดสอบ (หัวหน้า)');
  });

  it('says where it happened, as far as the server knows', () => {
    expect(alertDetail(blockedAlert)).toBe(
      'ป้าย AR12-02 ห้องน้ำหญิง ของ AR12 Area สิบสอง · พื้นที่ของแม่บ้านที่อื่นคือ AR03 · ไม่ได้บันทึกงาน',
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

  it('describes a cover candidate and a made cover in words', () => {
    expect(candidates.map((candidate) => candidateText(candidate))).toEqual([
      'แม่บ้านที่อื่น · AR03 · เข้างานแล้ว 06:55 · สแกนป้ายพื้นที่นี้เมื่อ 09:10',
      'แม่บ้านทดสอบ · AR01 · เข้างานแล้ว 06:52',
      'แม่บ้านมาสาย · AR08 · ยังไม่ลงเวลา',
    ]);
    expect(coverNotice({ ...made, areaId: 12, areaCode: 'AR12' })).toBe('มอบหมาย แม่บ้านทดสอบ ทำแทน AR12 แล้ว');
    expect(coverNotice(supervisorCover)).toBe('มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B แล้ว');
  });

  it('names the building of a Supervisor cover with its current name when it is known (facility-0091)', () => {
    expect(coverNotice(supervisorCover, new Map([['B', 'ตึกข้างเคียง']]))).toBe(
      'มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B · ตึกข้างเคียง แล้ว',
    );
  });
});
