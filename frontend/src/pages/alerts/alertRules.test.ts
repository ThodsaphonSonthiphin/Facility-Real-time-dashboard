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
  alertDay,
  alertDetail,
  alertResult,
  alertRole,
  alertTitle,
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

  it('splits by kind, by day and by talked-about or not; the three GPS flags are one kind', () => {
    const today = '2026-10-15';
    expect(alertDay(attendanceGpsAlert, today)).toBe('today'); // 06:52 Thai time
    expect(alertDay(reviewedAlert, today)).toBe('yesterday');
    expect(alertDay(reviewedAlert, '2026-10-16')).toBeNull();
    expect(filterAlerts(alerts, { tab: 'far', review: 'open', day: 'today', today })).toEqual([
      inspectionGpsAlert,
      attendanceGpsAlert,
    ]);
    expect(filterAlerts(alerts, { tab: 'far', review: 'reviewed', day: 'today', today })).toEqual([]);
    expect(filterAlerts(alerts, { tab: 'far', review: 'all', day: 'yesterday', today })).toEqual([reviewedAlert]);
    expect(filterAlerts(alerts, { tab: 'blocked', review: 'open', day: 'today', today })).toEqual([blockedAlert]);
    expect(filterAlerts(alerts, { tab: 'missing', review: 'all', day: 'today', today })).toEqual([missingAlert]);
  });

  it("names an alert in the design file's words; a Blocked Scan says why it was refused", () => {
    expect(alertTitle(blockedAlert)).toBe('สแกนผิดพื้นที่');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OtherBuilding' })).toBe('สแกนผิดพื้นที่ (ผิดตึก)');
    expect(alertTitle({ ...blockedAlert, blockReason: 'OutsideShift' })).toBe('สแกนผิดพื้นที่ (นอกกะ)');
    expect(alertTitle(inspectionGpsAlert)).toBe('ตรวจงานไกลป้าย');
    expect(alertTitle(missingAlert)).toBe('ลืมลงเวลา');
    expect(alertRole(inspectionGpsAlert)).toBe('หัวหน้า');
    expect(alertRole(blockedAlert)).toBe('แม่บ้าน · AR03');
    expect(alertResult(blockedAlert)).toEqual({ text: 'ไม่บันทึก', refused: true });
    expect(alertResult(attendanceGpsAlert)).toEqual({ text: 'บันทึก ติดธง', refused: false });
    expect(alertResult(missingAlert)).toEqual({ text: 'รอเติมเวลา', refused: false });
  });

  it('says where it happened, as far as the server knows', () => {
    expect(alertDetail(blockedAlert)).toBe('สแกนป้าย AR12-02 ห้องน้ำหญิง ของ AR12 Area สิบสอง · AR12 ยังไม่มีคน');
    expect(alertDetail({ ...blockedAlert, cover: undefined })).toBe('สแกนป้าย AR12-02 ห้องน้ำหญิง ของ AR12 Area สิบสอง');
    expect(alertDetail(attendanceGpsAlert)).toBe(
      'ป้ายลงเวลา AR01-IN · AR01 Area ทดลอง · ห่าง 85 ม. (รัศมี 50 ม.) · ความแม่นยำ ±40 ม.',
    );
    expect(alertDetail({ ...attendanceGpsAlert, accuracyM: undefined, radiusM: undefined })).toBe(
      'ป้ายลงเวลา AR01-IN · AR01 Area ทดลอง · ห่าง 85 ม.',
    );
    expect(alertDetail(missingAlert)).toBe('ไม่ได้ลงเวลาเลิกงาน · กะดึก 14/10/2026 · AR01 Area ทดลอง');
    expect(alertDetail({ ...missingAlert, missingEvents: ['BreakIn', 'ShiftOut'] })).toBe(
      'ไม่ได้ลงเวลากลับจากพัก และเลิกงาน · กะดึก 14/10/2026 · AR01 Area ทดลอง',
    );
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
