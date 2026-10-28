/**
 * facility-0094: the Development seed the end-to-end run starts from, copied from DbInitializer.cs. Made-up people and
 * numbers (the repo is public). The only copy in the tests: change it here when the seed changes.
 */
export interface Employee {
  employeeId: string;
  phone: string;
  name: string;
}

/** The Area every test looks at (its heading starts with this code). */
export const areaCode = 'AR01';
export const admin = { username: 'admin', password: 'admin1234' };
export const supervisor: Employee = { employeeId: 'S2001', phone: '0820000001', name: 'สมปอง ตรวจดี' };
const dayCleaner: Employee = { employeeId: 'E1001', phone: '0810000001', name: 'สมชาย ใจดี' };
const nightCleaner: Employee = { employeeId: 'E1002', phone: '0810000002', name: 'สมหญิง รักสะอาด' };

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
