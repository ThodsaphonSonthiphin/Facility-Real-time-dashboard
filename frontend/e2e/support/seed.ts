/**
 * facility-0094: the Development seed the end-to-end run starts from, copied from DbInitializer.cs. Made-up people and
 * numbers (the repo is public). The only copy in the tests: change it here when the seed changes.
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export interface Employee {
  employeeId: string;
  phone: string;
  name: string;
}

/** The Area every test looks at (its heading starts with this code). */
export const areaCode = 'AR01';
// Read from DbInitializer.cs at run time, so no password text sits in this file (GitGuardian flags password literals)
const seedSource = readFileSync(
  fileURLToPath(new URL('../../../backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs', import.meta.url)),
  'utf8',
);
const adminPassword = /Username = "admin"[^\n]*?Hash\("([^"]+)"\)/.exec(seedSource)?.[1];
if (!adminPassword) throw new Error('seed.ts: could not find the seed Admin password in DbInitializer.cs');
export const admin = { username: 'admin', password: adminPassword };
export const supervisor: Employee = { employeeId: '2000001', phone: '0820000001', name: 'สมปอง ตรวจดี' };
const dayCleaner: Employee = { employeeId: '1000001', phone: '0810000001', name: 'สมชาย ใจดี' };
const nightCleaner: Employee = { employeeId: '1000002', phone: '0810000002', name: 'สมหญิง รักสะอาด' };

/** AR01's check-in sign, and where the phone stands (the sign's own location, so the distance is 0). */
export const checkInToken = 'token-checkin-ar01';
export const signLocation = { latitude: 13.7563, longitude: 100.5018, accuracy: 5 };

/** A point sign of AR01, about 7 m from the check-in sign: inside the radius. */
export const restroomToken = 'token-restroom-m1';
export const restroomName = 'ห้องน้ำชาย ชั้น 1';

/** The hour on a Thai wall clock (UTC+7, no daylight saving). */
export function thaiHour(now: Date = new Date()): number {
  return (now.getUTCHours() + 7) % 24;
}

/** Day shift 07:00–19:00, Night 19:00–07:00 (ShiftCalendar): the AR01 Cleaner whose shift the clock is in. */
export function cleanerOnShift(now: Date = new Date()): Employee {
  const hour = thaiHour(now);
  return hour >= 7 && hour < 19 ? dayCleaner : nightCleaner;
}

/** 19:00–20:00: the Night shift has started, its first restroom round (20:00) has not, so D1 shows no submission. */
export function beforeFirstNightRound(now: Date = new Date()): boolean {
  return thaiHour(now) === 19;
}

/** The last two minutes before 07:00 or 19:00: a multi-step workflow could cross its shift change. */
export function nearShiftChange(now: Date = new Date()): boolean {
  return [6, 18].includes(thaiHour(now)) && now.getUTCMinutes() >= 58;
}

const pad = (value: number, width: number) => String(value).padStart(width, '0');

/**
 * facility-0102: generated Area n (E2EA000n, from 1), its signs and the Cleaner of the shift on the clock. Every
 * generated sign stands where AR01's check-in sign does, and every generated account has the Day Cleaner's synthetic
 * phone. Area n works both shifts unless n is a multiple of 4. Each case owns its Area: 1 rework, 2 covers,
 * 3 attendance offline, 5 GPS far, 6 submission offline, 7 location off.
 */
export function factoryArea(n: number, now: Date = new Date()) {
  const hour = thaiHour(now);
  const number = (n - 1) * 2 + (hour < 7 || hour >= 19 ? 1 : 0);
  return {
    code: `E2EA${pad(n, 4)}`,
    checkIn: `/check-in/e2e-checkin-${n}`,
    point: (p: number) => `/scan/e2e-point-${n}-${p}`,
    cleaner: { employeeId: `7${pad(number, 6)}`, phone: dayCleaner.phone },
  };
}
