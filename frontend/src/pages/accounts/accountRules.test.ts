import { describe, expect, it } from 'vitest';
import { account } from '../../test/adminFixtures';
import { createRequest, emptyDraft, normalizePhone, placeText } from './accountRules';

describe('account rules (facility-0020, 0054)', () => {
  it('builds a request with only the role’s own fields (the server refuses the others)', () => {
    const typed = {
      displayName: ' แม่บ้านใหม่ ',
      employeeId: '650010',
      phone: '081-000-0009',
      areaId: '2',
      buildingId: '1',
      shift: 'Day' as const,
      username: 'left-over',
      password: 'left-over-secret',
    };
    expect(createRequest({ ...typed, role: 'Cleaner' })).toEqual({
      request: { role: 'Cleaner', displayName: 'แม่บ้านใหม่', employeeId: '650010', phone: '0810000009', shift: 'Day', areaId: 2 },
    });
    expect(createRequest({ ...typed, role: 'Cleaner', areaId: '' })).toEqual({
      request: { role: 'Cleaner', displayName: 'แม่บ้านใหม่', employeeId: '650010', phone: '0810000009', shift: 'Day' },
    });
    expect(createRequest({ ...typed, role: 'Supervisor' })).toEqual({
      request: { role: 'Supervisor', displayName: 'แม่บ้านใหม่', employeeId: '650010', phone: '0810000009', buildingId: 1, shift: 'Day' },
    });
    expect(createRequest({ ...typed, role: 'Admin' })).toEqual({
      request: { role: 'Admin', displayName: 'แม่บ้านใหม่', username: 'left-over', password: 'left-over-secret' },
    });
    expect(createRequest(emptyDraft())).toEqual({ problem: 'ใส่ชื่อที่แสดงก่อน' });
  });

  it('accepts a phone of 10 Arabic digits starting with 0, ignoring spaces and dashes', () => {
    expect(normalizePhone('081-000 0009')).toBe('0810000009');
    expect(normalizePhone('810000009')).toBeNull();
    expect(normalizePhone('08100000099')).toBeNull();
    expect(normalizePhone('๐๘๑๐๐๐๐๐๐๙')).toBeNull();
  });

  it('writes where each role works', () => {
    expect(placeText(account())).toBe('AR01 · กะเช้า');
    expect(placeText(account({ areaId: undefined, areaCode: undefined, shift: 'Night' }))).toBe('ยังไม่มี Area · กะดึก');
    expect(placeText(account({ role: 'Supervisor', areaCode: undefined, buildingCode: 'B', shift: 'Night' }))).toBe(
      'ตึก B · กะดึก',
    );
    expect(placeText(account({ role: 'Admin', areaCode: undefined, shift: undefined }))).toBe('—');
  });
});
