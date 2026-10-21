import { describe, expect, it } from 'vitest';
import { firstPage, olderPage } from '../../test/reportFixtures';
import { actionText, changeLines, readHistoryArgs, searchEntries, toHistoryParams } from './historyRules';

describe('historyRules (D9)', () => {
  it('actionText names each action in Thai, and shows an unknown one as stored', () => {
    expect(actionText('ATTENDANCE_EDIT')).toBe('แก้เวลาลงงาน');
    expect(actionText('ACCOUNT_PHONE')).toBe('แก้เบอร์โทร');
    expect(actionText('SIGN_LOCATION')).toBe('เก็บพิกัดป้าย');
    expect(actionText('SOMETHING_NEW')).toBe('SOMETHING_NEW');
    expect(actionText('constructor')).toBe('constructor');
  });

  it('readHistoryArgs keeps a range given in full, a real actor id and a known type', () => {
    expect(readHistoryArgs(new URLSearchParams('from=2026-10-01&to=2026-10-07&actor=8&category=Cover'))).toEqual({
      from: '2026-10-01',
      to: '2026-10-07',
      actorId: 8,
      category: 'Cover',
    });
    expect(readHistoryArgs(new URLSearchParams('from=2026-10-01&actor=x&category=Nope'))).toEqual({});
  });

  it('toHistoryParams writes what readHistoryArgs reads, leaving out what is not chosen', () => {
    expect(toHistoryParams({ from: '2026-10-01', to: '2026-10-07', actorId: 8, category: 'Cover' }).toString()).toBe(
      'from=2026-10-01&to=2026-10-07&actor=8&category=Cover',
    );
    expect(toHistoryParams({ category: 'Export' }).toString()).toBe('category=Export');
    expect(toHistoryParams({}).toString()).toBe('');
  });

  it('changeLines: one line per changed field, in Thai, from the stored snapshots (plan 12 decision 8)', () => {
    const [added, cover, review, phone, exported] = firstPage.entries;
    expect(changeLines(added)).toEqual(['รายการ: – → เข้างาน', 'เวลา: – → 15/10 06:58', 'ที่มา: – → Admin เพิ่ม']);
    // null fields (BuildingId, BlockedScanId) are "–" on both sides, so they are left out
    expect(changeLines(cover)).toEqual([
      'คนทำแทน (รหัสในระบบ): – → 25',
      'Area (รหัสในระบบ): – → 1',
      'วันที่ของกะ: – → 15/10/2026',
      'กะ: – → กะเช้า',
    ]);
    expect(changeLines(review)).toEqual(['คุยแล้ว: ไม่ใช่ → ใช่', 'ประเภทเตือน: – → ลงเวลาไกลจากป้าย']);
    expect(changeLines(phone)).toEqual(['เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)']);
    expect(changeLines(exported)).toEqual([
      'ตั้งแต่: – → 01/10/2026',
      'ถึง: – → 05/10/2026',
      'ชุดข้อมูล: – → การลงเวลา, การส่งงาน',
      'จำนวนแถว: – → 28, 412',
    ]);
    expect(changeLines(olderPage.entries[0])).toEqual(['ออก QR: 01/10 09:00 → 13/10 14:40']);
  });

  it('changeLines leaves out fields that did not change, and shows round windows and Area shifts in Thai', () => {
    expect(
      changeLines({
        action: 'ATTENDANCE_EDIT',
        before: { EventType: 'ShiftOut', OccurredAt: '2026-10-15T11:58:00Z', Source: 'Scan' },
        after: { EventType: 'ShiftOut', OccurredAt: '2026-10-15T11:30:00Z', Source: 'AdminEdit' },
      }),
    ).toEqual(['เวลา: 15/10 18:58 → 15/10 18:30', 'ที่มา: สแกนเอง → แก้โดย Admin']);
    expect(
      changeLines({
        action: 'POINT_UPDATE',
        before: { Name: 'ห้องน้ำชาย', RoundWindows: ['DAY 13:00-15:00'] },
        after: { Name: 'ห้องน้ำชาย', RoundWindows: ['DAY 13:30-15:30', 'NIGHT 20:00-22:00'] },
      }),
    ).toEqual(['ช่วงรอบ: เช้า 13:00-15:00 → เช้า 13:30-15:30, ดึก 20:00-22:00']);
    const area = { Code: 'AR01', Name: 'Area ทดลอง', BuildingId: 1, ShiftPattern: 'DayAndNight', DayCleanerId: 21, NightCleanerId: 22, RemovedNightWindows: null };
    expect(
      changeLines({
        action: 'AREA_UPDATE',
        before: area,
        after: { ...area, ShiftPattern: 'DayOnly', NightCleanerId: null, RemovedNightWindows: 2 },
      }),
    ).toEqual(['กะของ Area: 2 กะ (เช้า + ดึก) → เฉพาะกะเช้า', 'แม่บ้านกะดึก (รหัสในระบบ): 22 → –', 'รอบกะดึกที่ถูกลบ: – → 2']);
    // the server always stores the count after an Area edit; none removed is no change
    expect(changeLines({ action: 'AREA_UPDATE', before: area, after: { ...area, Name: 'Area ใหม่', RemovedNightWindows: 0 } })).toEqual([
      'ชื่อ: Area ทดลอง → Area ใหม่',
    ]);
    expect(
      changeLines({ action: 'COVER_CANCEL', before: { cancelled: false }, after: { cancelled: true, alsoCancelled: [] } }),
    ).toEqual(['ยกเลิกแล้ว: ไม่ใช่ → ใช่']);
  });

  it('changeLines hides secrets and shows nothing when nothing was stored', () => {
    expect(changeLines({ action: 'ACCOUNT_PASSWORD' })).toEqual(['รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)']);
    expect(changeLines({ action: 'SOMETHING_NEW' })).toEqual([]);
    expect(changeLines({ action: 'SOMETHING_NEW', before: { Extra: 1 }, after: { Extra: 2 } })).toEqual(['Extra: 1 → 2']);
  });

  it('searchEntries keeps the rows whose summary, actor, reason or type matches, in any letter case', () => {
    const entries = firstPage.entries;
    expect(searchEntries(entries, '  ').map((entry) => entry.id)).toEqual([50, 49, 48, 47, 46]);
    expect(searchEntries(entries, 'แม่บ้านที่อื่น').map((entry) => entry.id)).toEqual([49]);
    expect(searchEntries(entries, 'ผู้ดูแลคนที่สอง').map((entry) => entry.id)).toEqual([49]);
    expect(searchEntries(entries, 'gps').map((entry) => entry.id)).toEqual([48]);
    expect(searchEntries(entries, 'แก้เบอร์').map((entry) => entry.id)).toEqual([47]);
  });
});
