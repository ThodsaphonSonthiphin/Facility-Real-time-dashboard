import type {
  AdminPoint,
  AdminSign,
  AreaDetail,
  AreaSummary,
  Building,
  CleanerOption,
  ManagedBuilding,
} from '../shared/api/adminSetupApi';
import type { Account } from '../pages/accounts/accountsApi';
import { authAnswer } from './fakeServer';

/**
 * Made-up answers for the Admin setup pages (public repo: no real names). The API sends UTC; 02:00Z is 09:00 in
 * Thailand. Fields the server sends as null are left out, as it does (an `undefined` override is dropped by JSON).
 */
export const adminSession = { 'POST /api/auth/refresh': () => authAnswer('admin') };

export const buildings: Building[] = [
  { id: 1, code: 'A', name: 'ตึกทดลอง' },
  { id: 2, code: 'B', name: 'ตึกข้างเคียง' },
];

/** GET /api/admin/buildings/all (facility-0090): A holds two open Areas and a Supervisor, B is empty, C is off. */
export const managedBuildings: ManagedBuilding[] = [
  { id: 1, code: 'A', name: 'ตึกทดลอง', isActive: true, activeAreaCount: 2, activeSupervisorCount: 1 },
  { id: 2, code: 'B', name: 'ตึกข้างเคียง', isActive: true, activeAreaCount: 0, activeSupervisorCount: 0 },
  { id: 3, code: 'C', name: 'ตึกเก่า', isActive: false, activeAreaCount: 0, activeSupervisorCount: 0 },
];

/** Active Cleaners: two hold AR01, two have no Area yet, one holds an Area of another building. */
export const cleaners: CleanerOption[] = [
  { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', shift: 'Day', areaId: 1, areaCode: 'AR01' },
  { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก', shift: 'Night', areaId: 1, areaCode: 'AR01' },
  { id: 23, employeeId: '650003', displayName: 'แม่บ้านว่าง', shift: 'Day' },
  { id: 24, employeeId: '650004', displayName: 'แม่บ้านว่างกะดึก', shift: 'Night' },
  { id: 25, employeeId: '650005', displayName: 'แม่บ้านที่อื่น', shift: 'Day', areaId: 3, areaCode: 'AR03' },
];

/** A point's sign captured on site, ±14 m, radius 50 m; QR issued 09:00 on 16/10/2026. */
export function sign(overrides: Partial<AdminSign> = {}): AdminSign {
  return {
    id: 101,
    code: 'AR01-01',
    qrToken: '0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f',
    qrIssuedAt: '2026-10-16T02:00:00Z',
    latitude: 13.7563,
    longitude: 100.5018,
    locationAccuracyM: 14,
    locationSource: 'Site',
    locatedAt: '2026-10-16T02:30:00Z',
    radiusM: 50,
    ...overrides,
  };
}

/** A sign with no location yet: the server leaves the location fields out. */
export function unlocatedSign(overrides: Partial<AdminSign> = {}): AdminSign {
  return {
    id: 102,
    code: 'AR01-02',
    qrToken: '5d1c9a2e-8f3b-4e6a-b7c0-2d4e6f8a0b1c',
    qrIssuedAt: '2026-10-16T02:00:00Z',
    radiusM: 50,
    ...overrides,
  };
}

/** AR01's check-in sign, captured on site. */
export function checkInSign(overrides: Partial<AdminSign> = {}): AdminSign {
  return sign({ id: 100, code: 'AR01-IN', qrToken: '9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d', ...overrides });
}

/** A point with two Day and two Night windows (the night 03:00 after 20:00, as the server orders them). */
export function adminPoint(overrides: Partial<AdminPoint> = {}): AdminPoint {
  return {
    id: 11,
    name: 'ห้องน้ำชาย',
    sortOrder: 1,
    isActive: true,
    sign: sign(),
    roundWindows: [
      { id: 1, shift: 'Day', start: '07:00', end: '09:00' },
      { id: 2, shift: 'Day', start: '16:00', end: '18:00' },
      { id: 3, shift: 'Night', start: '20:00', end: '22:00' },
      { id: 4, shift: 'Night', start: '03:00', end: '05:00' },
    ],
    ...overrides,
  };
}

/** AR01: two shifts, a Cleaner in each, two active points, one sign not confirmed on site. */
export function areaSummary(overrides: Partial<AreaSummary> = {}): AreaSummary {
  return {
    id: 1,
    code: 'AR01',
    name: 'Area ทดลอง',
    buildingId: 1,
    buildingCode: 'A',
    shiftPattern: 'DayAndNight',
    isActive: true,
    dayCleaner: { id: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ' },
    nightCleaner: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก' },
    activePointCount: 2,
    signsNotConfirmedOnSite: 1,
    ...overrides,
  };
}

/** AR02: day only, no Cleaner, no point; its check-in sign has no location. */
export const officeSummary: AreaSummary = areaSummary({
  id: 2,
  code: 'AR02',
  name: 'Office ทดลอง',
  shiftPattern: 'DayOnly',
  dayCleaner: undefined,
  nightCleaner: undefined,
  activePointCount: 0,
  signsNotConfirmedOnSite: 1,
});

/** AR01 in full: the check-in sign, ห้องน้ำชาย (located) and ห้องน้ำหญิง (no location, one Day window). */
export function areaDetail(overrides: Partial<AreaDetail> = {}): AreaDetail {
  return {
    area: areaSummary(),
    checkInSign: checkInSign(),
    points: [
      adminPoint(),
      adminPoint({
        id: 12,
        name: 'ห้องน้ำหญิง',
        sortOrder: 2,
        sign: unlocatedSign(),
        roundWindows: [{ id: 5, shift: 'Day', start: '08:00', end: '10:00' }],
      }),
    ],
    ...overrides,
  };
}

/** AR02 in full: only its check-in sign. */
export function officeDetail(overrides: Partial<AreaDetail> = {}): AreaDetail {
  return {
    area: officeSummary,
    checkInSign: unlocatedSign({ id: 200, code: 'AR02-IN', qrToken: '1f2e3d4c-5b6a-4987-8a6b-5c4d3e2f1a0b' }),
    points: [],
    ...overrides,
  };
}

/** A Cleaner of AR01, Day shift (AccountDto). */
export function account(overrides: Partial<Account> = {}): Account {
  return {
    id: 21,
    role: 'Cleaner',
    loginName: '650001',
    displayName: 'แม่บ้านทดสอบ',
    areaId: 1,
    areaCode: 'AR01',
    shift: 'Day',
    isActive: true,
    createdAt: '2026-10-01T02:00:00Z',
    ...overrides,
  };
}

const noPlace = { areaId: undefined, areaCode: undefined, shift: undefined };

/**
 * The server's order: Admins, Supervisors, Cleaners, each by login name. Account 7 is the logged-in Admin
 * (authAnswer('admin')).
 */
export const accounts: Account[] = [
  account({ id: 7, role: 'Admin', loginName: 'admin', displayName: 'ผู้ดูแลทดสอบ', ...noPlace }),
  account({ id: 8, role: 'Admin', loginName: 'admin2', displayName: 'ผู้ดูแลคนที่สอง', ...noPlace }),
  account({
    id: 31,
    role: 'Supervisor',
    loginName: '660001',
    displayName: 'หัวหน้าทดสอบ',
    areaId: undefined,
    areaCode: undefined,
    buildingId: 1,
    buildingCode: 'A',
  }),
  account(),
  account({ id: 22, loginName: '650002', displayName: 'แม่บ้านกะดึก', shift: 'Night' }),
  account({ id: 23, loginName: '650003', displayName: 'แม่บ้านว่าง', areaId: undefined, areaCode: undefined }),
  account({
    id: 26,
    loginName: '650006',
    displayName: 'แม่บ้านที่ลาออก',
    areaId: undefined,
    areaCode: undefined,
    isActive: false,
  }),
];
