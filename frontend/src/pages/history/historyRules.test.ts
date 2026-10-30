import { describe, expect, it } from 'vitest';
import { firstPage, olderPage } from '../../test/reportFixtures';
import { actionText, changeLines, changeValues, readHistoryArgs, toHistoryParams } from './historyRules';

describe('historyRules (D9)', () => {
  it('keeps arrows and HTML-like text inside their original value, with compatible text output', () => {
    const entry = { action: 'POINT_UPDATE', before: { Name: 'A → B' }, after: { Name: '<b>C → D</b>' } };
    expect(changeValues(entry)).toEqual([{ label: 'ชื่อ', before: 'A → B', after: '<b>C → D</b>' }]);
    expect(changeLines(entry)).toEqual(['ชื่อ: A → B → <b>C → D</b>']);
  });

  it('handles null, primitive and unchanged snapshots without inventing a change', () => {
    expect(changeValues({ action: 'NEW', before: null, after: null })).toEqual([]);
    expect(changeValues({ action: 'NEW', before: 4, after: 5 })).toEqual([{ before: '4', after: '5' }]);
    expect(changeValues({ action: 'NEW', before: 'same', after: 'same' })).toEqual([]);
    expect(changeValues({ action: 'NEW', before: { Name: 'same' }, after: { Name: 'same' } })).toEqual([]);
    expect(changeValues({ action: 'EXPORT_EXCEL', after: { BuildingIds: [2, 8] } })).toEqual([
      { label: 'ตึกที่เลือก (รหัสในระบบ)', before: '–', after: '2, 8' },
    ]);
  });

  it.each(['ACCOUNT_PHONE', 'ACCOUNT_PASSWORD', 'ADMIN_RECOVER_RESET'])('suppresses arbitrary secret snapshots for %s', (action) => {
    const values = changeValues({ action, before: { Secret: 'hidden-before' }, after: { Secret: 'hidden-after' } });
    expect(JSON.stringify(values)).not.toContain('hidden-');
    expect(changeLines({ action })).toHaveLength(1);
  });
  it('actionText names each action in Thai, and shows an unknown one as stored', () => {
    expect(actionText('ATTENDANCE_EDIT')).toBe('แก้เวลาลงงาน');
    expect(actionText('ACCOUNT_PHONE')).toBe('แก้เบอร์โทร');
    expect(actionText('SIGN_LOCATION')).toBe('เก็บพิกัดป้าย');
    expect(actionText('BUILDING_DEACTIVATE')).toBe('ปิดใช้ตึก');
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
    expect(readHistoryArgs(new URLSearchParams('q=%20AR01%20'))).toEqual({ q: 'AR01' });
    expect(readHistoryArgs(new URLSearchParams('q=%20%20'))).toEqual({});
    expect(readHistoryArgs(new URLSearchParams(`q=${'a'.repeat(101)}`))).toEqual({});
  });

  it('toHistoryParams writes what readHistoryArgs reads, leaving out what is not chosen', () => {
    expect(toHistoryParams({ from: '2026-10-01', to: '2026-10-07', actorId: 8, category: 'Cover' }).toString()).toBe(
      'from=2026-10-01&to=2026-10-07&actor=8&category=Cover',
    );
    expect(toHistoryParams({ category: 'Export' }).toString()).toBe('category=Export');
    expect(toHistoryParams({ category: 'Export', q: ' AR01 ' }).toString()).toBe('category=Export&q=AR01');
    expect(toHistoryParams({ q: '  ' }).toString()).toBe('');
    expect(toHistoryParams({}).toString()).toBe('');
  });

  it('changeLines: one line per changed field, in Thai, from the stored snapshots (plan 12 decision 8)', () => {
    const [added, cover, review, phone, exported] = firstPage.entries;
    expect(changeLines(added)).toEqual(['รายการ: – → เข้างาน', 'เวลา: – → 15/10 06:58', 'ที่มา: – → แอดมินเพิ่ม']);
    // null fields (BuildingId, BlockedScanId) are "–" on both sides, so they are left out
    expect(changeLines(cover)).toEqual([
      'คนทำแทน (รหัสในระบบ): – → 25',
      'พื้นที่ (รหัสในระบบ): – → 1',
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
    expect(changeLines(olderPage.entries[0])).toEqual(['สร้าง QR: 01/10 09:00 → 13/10 14:40']);
  });

  it('changeLines leaves out fields that did not change, and shows round windows and Area shifts in Thai', () => {
    expect(
      changeLines({
        action: 'ATTENDANCE_EDIT',
        before: { EventType: 'ShiftOut', OccurredAt: '2026-10-15T11:58:00Z', Source: 'Scan' },
        after: { EventType: 'ShiftOut', OccurredAt: '2026-10-15T11:30:00Z', Source: 'AdminEdit' },
      }),
    ).toEqual(['เวลา: 15/10 18:58 → 15/10 18:30', 'ที่มา: สแกนเอง → แก้โดยแอดมิน']);
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
    ).toEqual(['กะของพื้นที่: 2 กะ (เช้า + ดึก) → เฉพาะกะเช้า', 'แม่บ้านกะดึก (รหัสในระบบ): 22 → –', 'รอบกะดึกที่ถูกลบ: – → 2']);
    // the server always stores the count after an Area edit; none removed is no change
    expect(changeLines({ action: 'AREA_UPDATE', before: area, after: { ...area, Name: 'Area ใหม่', RemovedNightWindows: 0 } })).toEqual([
      'ชื่อ: Area ทดลอง → Area ใหม่',
    ]);
    expect(
      changeLines({ action: 'COVER_CANCEL', before: { cancelled: false }, after: { cancelled: true, alsoCancelled: [] } }),
    ).toEqual(['ยกเลิกแล้ว: ไม่ใช่ → ใช่']);
  });

  it('names what the server did by itself, and hides the password it reset (facility-0092)', () => {
    expect(actionText('ADMIN_FIRST_CREATE')).toBe('สร้างแอดมินคนแรกจากค่าในเซิร์ฟเวอร์');
    expect(actionText('ADMIN_RECOVER_CREATE')).toBe('สร้างแอดมินด้วยคำสั่ง reset-admin');
    expect(actionText('ADMIN_RECOVER_RESET')).toBe('กู้คืนแอดมินด้วยคำสั่ง reset-admin');
    expect(changeLines({ action: 'ADMIN_RECOVER_RESET' })).toEqual(['รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)']);
  });

  it('changeLines hides secrets and shows nothing when nothing was stored', () => {
    expect(changeLines({ action: 'ACCOUNT_PASSWORD' })).toEqual(['รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)']);
    expect(changeLines({ action: 'SOMETHING_NEW' })).toEqual([]);
    expect(changeLines({ action: 'SOMETHING_NEW', before: { Extra: 1 }, after: { Extra: 2 } })).toEqual(['Extra: 1 → 2']);
  });

  it('changeLines: typed text that looks like a time or date shows as stored, and never throws', () => {
    expect(
      changeLines({ action: 'ACCOUNT_UPDATE', before: { DisplayName: 'แม่บ้านทดสอบ' }, after: { DisplayName: '2026-13-45T25:61Z' } }),
    ).toEqual(['ชื่อที่แสดง: แม่บ้านทดสอบ → 2026-13-45T25:61Z']);
    expect(changeLines({ action: 'POINT_UPDATE', before: { Name: 'ห้องน้ำชาย' }, after: { Name: '2026-10-15' } })).toEqual([
      'ชื่อ: ห้องน้ำชาย → 2026-10-15',
    ]);
  });

  it('changeLines: a time field that does not parse shows as stored', () => {
    expect(
      changeLines({ action: 'QR_REGENERATE', before: { QrIssuedAt: 'not-a-time' }, after: { QrIssuedAt: '2026-10-13T07:40:00Z' } }),
    ).toEqual(['สร้าง QR: not-a-time → 13/10 14:40']);
  });

  it('changeLines: a phone change never shows the number, whatever the snapshot holds', () => {
    expect(changeLines({ action: 'ACCOUNT_PHONE', before: { Phone: '0812345678' }, after: { Phone: '0899999999' } })).toEqual([
      'เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)',
    ]);
  });
});
