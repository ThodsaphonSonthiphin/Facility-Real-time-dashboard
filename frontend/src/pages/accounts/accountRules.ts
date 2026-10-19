import type { AreaSummary } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import { shiftShortLabel } from '../../shared/utils/labels';
import type { Account, AccountRole, CreateAccountRequest, UpdateAccountBody } from './accountsApi';

export type AccountFilter = 'all' | AccountRole | 'inactive';

export const accountFilters: readonly AccountFilter[] = ['all', 'Cleaner', 'Supervisor', 'Admin', 'inactive'];

/** Wireframe D6's tabs. */
export const filterLabel: Record<AccountFilter, string> = {
  all: 'ทั้งหมด',
  Cleaner: 'แม่บ้าน',
  Supervisor: 'หัวหน้า',
  Admin: 'Admin',
  inactive: 'ปิดใช้งาน',
};

export const accountRoleLabel: Record<AccountRole, string> = {
  Cleaner: 'แม่บ้าน',
  Supervisor: 'หัวหน้า',
  Admin: 'Admin',
};

function inFilter(account: Account, filter: AccountFilter): boolean {
  if (filter === 'all') {
    return true;
  }
  return filter === 'inactive' ? !account.isActive : account.role === filter;
}

/** facility-0056: about 180 rows, filtered on the page. The search covers the name and the login name. */
export function filterAccounts(accounts: Account[], filter: AccountFilter, search: string): Account[] {
  const needle = search.trim().toLowerCase();
  return accounts.filter(
    (account) =>
      inFilter(account, filter) &&
      (needle === '' ||
        account.displayName.toLowerCase().includes(needle) ||
        account.loginName.toLowerCase().includes(needle)),
  );
}

export function filterCounts(accounts: Account[]): Record<AccountFilter, number> {
  return Object.fromEntries(
    accountFilters.map((filter) => [filter, accounts.filter((account) => inFilter(account, filter)).length]),
  ) as Record<AccountFilter, number>;
}

/** D6's "ประจำที่" column. */
export function placeText(account: Account): string {
  if (account.role === 'Admin') {
    return '—';
  }
  const shift = account.shift ? shiftShortLabel[account.shift] : 'ไม่มีกะ';
  return account.role === 'Supervisor'
    ? `ตึก ${account.buildingCode ?? '-'} · ${shift}`
    : `${account.areaCode ?? 'ยังไม่มี Area'} · ${shift}`;
}

/** D6's Area option: "AR12 ชื่อ · ตึก B · 2 กะ · กะเช้ามีคนแล้ว (ทองใบ)". The account being edited is not "someone". */
export function areaOptionLabel(area: AreaSummary, exceptUserId?: number): string {
  const parts = [`${area.code} ${area.name}`, `ตึก ${area.buildingCode}`, area.shiftPattern === 'DayOnly' ? 'กะเช้าอย่างเดียว' : '2 กะ'];
  if (area.dayCleaner && area.dayCleaner.id !== exceptUserId) {
    parts.push(`กะเช้ามีคนแล้ว (${area.dayCleaner.displayName})`);
  }
  if (area.nightCleaner && area.nightCleaner.id !== exceptUserId) {
    parts.push(`กะดึกมีคนแล้ว (${area.nightCleaner.displayName})`);
  }
  if (!area.isActive) {
    parts.push('ปิดใช้งาน');
  }
  return parts.join(' · ');
}

/** Why a Cleaner cannot take this shift of this Area, or null (D6: "Area ละ 1 คนต่อกะ"; day-only has no night). */
export function cleanerShiftBlock(area: AreaSummary | undefined, shift: Shift, exceptUserId?: number): string | null {
  if (!area) {
    return null;
  }
  if (shift === 'Night' && area.shiftPattern === 'DayOnly') {
    return 'Area นี้ทำเฉพาะกะเช้า';
  }
  const holder = shift === 'Day' ? area.dayCleaner : area.nightCleaner;
  return holder && holder.id !== exceptUserId ? `มีคนประจำแล้ว (${holder.displayName})` : null;
}

/** Why a Supervisor cannot take this building-shift, or null (facility-0048: one per building per shift). */
export function supervisorShiftBlock(
  accounts: Account[],
  buildingId: number | undefined,
  shift: Shift,
  exceptUserId?: number,
): string | null {
  if (buildingId === undefined) {
    return null;
  }
  const holder = accounts.find(
    (other) =>
      other.role === 'Supervisor' &&
      other.isActive &&
      other.buildingId === buildingId &&
      other.shift === shift &&
      other.id !== exceptUserId,
  );
  return holder ? `มีหัวหน้าแล้ว (${holder.displayName})` : null;
}

/**
 * PhoneNumber.Normalize + IsComplete: keep only Arabic digits (so Thai digits vanish), turn a leading country code 66
 * of an 11-digit number into 0, then require 10 digits starting with 0.
 */
export function normalizePhone(text: string): string | null {
  const typed = text.replace(/[^0-9]/g, '');
  const digits = typed.length === 11 && typed.startsWith('66') ? `0${typed.slice(2)}` : typed;
  return /^0\d{9}$/.test(digits) ? digits : null;
}

export const PHONE_PROBLEM = 'เบอร์โทรต้องเป็นเลข 10 หลัก ขึ้นต้นด้วย 0';
export const MIN_PASSWORD = 8;
export const MAX_PASSWORD = 100;
export const PASSWORD_PROBLEM = 'รหัสผ่านชั่วคราวต้องยาว 8–100 ตัวอักษร';

/** The add form as typed; selects hold ids as strings, '' for none. */
export interface CreateDraft {
  role: AccountRole;
  displayName: string;
  employeeId: string;
  phone: string;
  areaId: string;
  buildingId: string;
  shift: Shift | '';
  username: string;
  password: string;
}

export function emptyDraft(role: AccountRole = 'Cleaner'): CreateDraft {
  return { role, displayName: '', employeeId: '', phone: '', areaId: '', buildingId: '', shift: '', username: '', password: '' };
}

/** The request with only the role's fields, or the first thing missing. The server checks the rest. */
export function createRequest(draft: CreateDraft): { request: CreateAccountRequest } | { problem: string } {
  const displayName = draft.displayName.trim();
  if (displayName === '') {
    return { problem: 'ใส่ชื่อที่แสดงก่อน' };
  }
  if (draft.role === 'Admin') {
    const username = draft.username.trim();
    if (username === '') {
      return { problem: 'ใส่ชื่อผู้ใช้ก่อน' };
    }
    if (draft.password.length < MIN_PASSWORD || draft.password.length > MAX_PASSWORD) {
      return { problem: PASSWORD_PROBLEM };
    }
    return { request: { role: 'Admin', displayName, username, password: draft.password } };
  }
  const employeeId = draft.employeeId.trim();
  if (employeeId === '') {
    return { problem: 'ใส่รหัสพนักงานก่อน' };
  }
  const phone = normalizePhone(draft.phone);
  if (!phone) {
    return { problem: PHONE_PROBLEM };
  }
  if (draft.role === 'Supervisor' && draft.buildingId === '') {
    return { problem: 'เลือกตึกก่อน' };
  }
  if (draft.shift === '') {
    return { problem: 'เลือกกะก่อน' };
  }
  if (draft.role === 'Supervisor') {
    return {
      request: { role: 'Supervisor', displayName, employeeId, phone, buildingId: Number(draft.buildingId), shift: draft.shift },
    };
  }
  return {
    request: {
      role: 'Cleaner',
      displayName,
      employeeId,
      phone,
      shift: draft.shift,
      ...(draft.areaId ? { areaId: Number(draft.areaId) } : {}),
    },
  };
}

/** The edit form as typed. */
export interface EditDraft {
  displayName: string;
  areaId: string;
  buildingId: string;
  shift: Shift | '';
}

export function editDraftOf(account: Account): EditDraft {
  return {
    displayName: account.displayName,
    areaId: account.areaId ? String(account.areaId) : '',
    buildingId: account.buildingId ? String(account.buildingId) : '',
    shift: account.shift ?? '',
  };
}

/**
 * The PUT replaces every place field, so a Cleaner's current Area is sent back to keep it (handoff note). A
 * deactivated Cleaner holds no Area (plan 5 decision 4), so none is sent for one.
 */
export function updateRequest(account: Account, draft: EditDraft): { body: UpdateAccountBody } | { problem: string } {
  const displayName = draft.displayName.trim();
  if (displayName === '') {
    return { problem: 'ใส่ชื่อที่แสดงก่อน' };
  }
  if (account.role === 'Admin') {
    return { body: { displayName } };
  }
  if (account.role === 'Supervisor' && draft.buildingId === '') {
    return { problem: 'เลือกตึกก่อน' };
  }
  if (draft.shift === '') {
    return { problem: 'เลือกกะก่อน' };
  }
  if (account.role === 'Supervisor') {
    return { body: { displayName, buildingId: Number(draft.buildingId), shift: draft.shift } };
  }
  return {
    body: {
      displayName,
      shift: draft.shift,
      ...(account.isActive && draft.areaId ? { areaId: Number(draft.areaId) } : {}),
    },
  };
}
