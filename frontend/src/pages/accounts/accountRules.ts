import type { AreaSummary } from '../../shared/api/adminSetupApi';
import { buildingText, type BuildingNames } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { shiftShortLabel } from '../../shared/utils/labels';
import type { Account, AccountRole, CreateAccountRequest, UpdateAccountBody } from './accountsApi';

import type { CleanerKindFields } from './accountFormTypes';

export type AccountActiveFilter = 'all' | 'active' | 'inactive';

export const accountRoles: readonly AccountRole[] = ['Cleaner', 'Supervisor', 'Admin'];

export const accountRoleLabel: Record<AccountRole, string> = {
  Cleaner: 'แม่บ้าน',
  Supervisor: 'หัวหน้า',
  Admin: 'แอดมิน',
};

/** facility-0056: role, active state and search independently narrow the locally loaded list. */
export function filterAccounts(accounts: Account[], role: AccountRole, active: AccountActiveFilter, search: string): Account[] {
  const needle = search.trim().toLowerCase();
  return accounts.filter(
    (account) =>
      account.role === role &&
      (active === 'all' || account.isActive === (active === 'active')) &&
      (needle === '' ||
        account.displayName.toLowerCase().includes(needle) ||
        account.loginName.toLowerCase().includes(needle)),
  );
}

/** Counts always describe the complete role, including inactive accounts, independently of the filters. */
export function filterCounts(accounts: Account[]): Record<AccountRole, number> {
  return Object.fromEntries(
    accountRoles.map((role) => [role, accounts.filter((account) => account.role === role).length]),
  ) as Record<AccountRole, number>;
}

/** D6's "ประจำที่" column. */
export function placeText(account: Account, names?: BuildingNames): string {
  if (account.role === 'Admin') {
    return '—';
  }
  const shift = account.shift ? shiftShortLabel[account.shift] : 'ไม่มีกะ';
  if (account.cleanerKind === 'Relief') {
    const home = account.homeBuildingCode ? buildingText(account.homeBuildingCode, names) : 'ยังไม่ทราบ';
    return `แม่บ้านสแปร์ · ตึกประจำ ${home} · ${shift}`;
  }
  if (account.role === 'Supervisor') {
    return `ตึก ${account.buildingCode ? buildingText(account.buildingCode, names) : '-'} · ${shift}`;
  }
  if (account.areaCode) {
    return `${account.areaCode} · ${account.shift ? shiftShortLabel[account.shift] : 'ยังไม่จัดกะ'}`;
  }
  const home = account.homeBuildingCode ? `ตึก ${buildingText(account.homeBuildingCode, names)} · ` : '';
  return account.shift
    ? `${home}ยังไม่มีพื้นที่ · ${shiftShortLabel[account.shift]}`
    : `${home}ยังไม่จัดกะและพื้นที่`;
}

export function areasForCleaner(
  areas: AreaSummary[],
  cleanerKind: CleanerKindFields['cleanerKind'],
  homeBuildingId: string | undefined,
): AreaSummary[] {
  return cleanerKind !== 'Relief' && homeBuildingId
    ? areas.filter((option) => String(option.buildingId) === homeBuildingId)
    : areas;
}

export function areaMatchesBuilding(areas: AreaSummary[], areaId: string, homeBuildingId: string): boolean {
  if (homeBuildingId === '' || areaId === '') return true;
  const current = areas.find((option) => String(option.id) === areaId);
  return current === undefined || String(current.buildingId) === homeBuildingId;
}

/** D6's Area option: "AR12 ชื่อ · ตึก B · 2 กะ · กะเช้ามีคนแล้ว (ทองใบ)". The account being edited is not "someone". */
export function areaOptionLabel(area: AreaSummary, exceptUserId?: number, names?: BuildingNames): string {
  const parts = [`${area.code} ${area.name}`, `ตึก ${buildingText(area.buildingCode, names)}`, area.shiftPattern === 'DayOnly' ? 'กะเช้าอย่างเดียว' : '2 กะ'];
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
    return 'พื้นที่นี้ทำเฉพาะกะเช้า';
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
export const MIN_PASSWORD = 15;
export const MAX_PASSWORD = 100;
export const PASSWORD_PROBLEM = 'รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร';

/** The add form as typed; selects hold ids as strings, '' for none. */
export interface CreateDraft extends CleanerKindFields {
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
  if (draft.role === 'Supervisor') {
    if (draft.buildingId === '') {
      return { problem: 'เลือกตึกก่อน' };
    }
    if (draft.shift === '') {
      return { problem: 'เลือกกะก่อน' };
    }
    return {
      request: { role: 'Supervisor', displayName, employeeId, phone, buildingId: Number(draft.buildingId), shift: draft.shift },
    };
  }
  const place = cleanerPlace(draft, draft.areaId, draft.shift);
  if ('problem' in place) return place;
  return {
    request: {
      role: 'Cleaner',
      displayName,
      employeeId,
      phone,
      ...place,
    },
  };
}

/** The edit form as typed. */
export interface EditDraft extends CleanerKindFields {
  displayName: string;
  areaId: string;
  buildingId: string;
  shift: Shift | '';
}

export function editDraftOf(account: Account, areas?: AreaSummary[]): EditDraft {
  const areaBuildingId = !account.homeBuildingId && account.areaId
    ? areas?.find((option) => option.id === account.areaId)?.buildingId
    : undefined;
  const homeBuildingId = account.homeBuildingId ?? areaBuildingId;
  return {
    displayName: account.displayName,
    areaId: account.areaId ? String(account.areaId) : '',
    buildingId: account.buildingId ? String(account.buildingId) : '',
    shift: account.shift ?? '',
    cleanerKind: account.cleanerKind,
    homeBuildingId: homeBuildingId ? String(homeBuildingId) : '',
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
  if (account.role === 'Supervisor') {
    if (draft.buildingId === '') {
      return { problem: 'เลือกตึกก่อน' };
    }
    if (draft.shift === '') {
      return { problem: 'เลือกกะก่อน' };
    }
    return { body: { displayName, buildingId: Number(draft.buildingId), shift: draft.shift } };
  }
  const place = cleanerPlace(draft, account.isActive ? draft.areaId : '', draft.shift, {
    wasRelief: account.cleanerKind === 'Relief',
    isActive: account.isActive,
  });
  if ('problem' in place) return place;
  return {
    body: {
      displayName,
      ...place,
    },
  };
}

function cleanerPlace(
  draft: CleanerKindFields,
  areaId: string,
  shift: Shift | '',
  options: { wasRelief?: boolean; isActive?: boolean } = {},
): Pick<UpdateAccountBody, 'areaId' | 'cleanerKind' | 'homeBuildingId' | 'shift'> | { problem: string } {
  const { wasRelief = false, isActive = true } = options;
  if (draft.cleanerKind === 'Relief') {
    if (!draft.homeBuildingId) {
      return { problem: 'เลือกตึกประจำก่อน' };
    }
    if (shift === '') {
      return { problem: 'เลือกกะก่อน' };
    }
    return { cleanerKind: 'Relief', homeBuildingId: Number(draft.homeBuildingId), shift };
  }
  if (areaId !== '') {
    if (shift === '') {
      return { problem: 'เลือกกะก่อน' };
    }
    return {
      areaId: Number(areaId),
      shift,
      ...(wasRelief ? { cleanerKind: 'Regular' as const } : {}),
    };
  }
  if (draft.homeBuildingId) {
    return {
      cleanerKind: 'Regular' as const,
      homeBuildingId: Number(draft.homeBuildingId),
      ...(shift ? { shift } : {}),
    };
  }
  // A deactivated pre-backfill Cleaner holds no Area and may have no homeBuildingId yet; keep shift-only updates valid.
  if (!isActive && !wasRelief) {
    if (shift === '') {
      return { problem: 'เลือกกะก่อน' };
    }
    return { shift };
  }
  return { problem: 'เลือกตึกประจำหรือพื้นที่ประจำก่อน' };
}
