import { describe, expect, it } from 'vitest';
import { account } from '../../test/adminFixtures';
import { createRequest, editDraftOf, emptyDraft, normalizePhone, placeText, updateRequest } from './accountRules';

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

  it('reads a phone as the server does: digits only, +66 becomes 0, then 10 digits starting with 0', () => {
    expect(normalizePhone('081-000 0009')).toBe('0810000009');
    expect(normalizePhone('+66 81 000 0009')).toBe('0810000009');
    expect(normalizePhone('(081) 000-0009')).toBe('0810000009');
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

  it('an edit sends the role’s place fields, and a Cleaner’s current Area to keep it (the PUT replaces it)', () => {
    const cleaner = account();
    expect(updateRequest(cleaner, editDraftOf(cleaner))).toEqual({
      body: { displayName: 'แม่บ้านทดสอบ', shift: 'Day', areaId: 1 },
    });
    const away = account({ isActive: false, areaId: undefined, areaCode: undefined });
    expect(updateRequest(away, { ...editDraftOf(away), areaId: '2' })).toEqual({
      body: { displayName: 'แม่บ้านทดสอบ', shift: 'Day' },
    });
    const supervisor = account({ role: 'Supervisor', areaId: undefined, buildingId: 2, shift: 'Night' });
    expect(updateRequest(supervisor, editDraftOf(supervisor))).toEqual({
      body: { displayName: 'แม่บ้านทดสอบ', buildingId: 2, shift: 'Night' },
    });
    const admin = account({ role: 'Admin', areaId: undefined, shift: undefined });
    expect(updateRequest(admin, editDraftOf(admin))).toEqual({ body: { displayName: 'แม่บ้านทดสอบ' } });
    expect(updateRequest(cleaner, { ...editDraftOf(cleaner), displayName: ' ' })).toEqual({ problem: 'ใส่ชื่อที่แสดงก่อน' });
  });
});
