import type { InspectionSummary, SummaryRow } from '../pages/inspection-summary/summaryApi';

/**
 * Made-up answers for the Admin report pages (public repo: no real names). The API sends UTC; the Thai time is in the
 * comments: 03:42Z is 10:42. Fields the server sends as null are left out, as it does.
 */

/** A Day Supervisor of building A who inspected every point of the beat at least once. */
export function summaryRow(overrides: Partial<SummaryRow> = {}): SummaryRow {
  return {
    supervisorId: 31,
    employeeId: '660001',
    displayName: 'หัวหน้าทดสอบ',
    buildingCode: 'A',
    shift: 'Day',
    pointsInspected: 12,
    pointsTotal: 12,
    submitted: 168,
    submittedInspected: 96,
    notInspected: 72,
    inspections: 101,
    reworks: 11,
    gpsFlagged: 3,
    ...overrides,
  };
}

/** The server's default range (the 7 dates to 15/10/2026), in its order: building, shift, employee ID. */
export const summary: InspectionSummary = {
  from: '2026-10-09',
  to: '2026-10-15',
  rows: [
    summaryRow(),
    summaryRow({
      supervisorId: 32,
      employeeId: '660002',
      displayName: 'หัวหน้ากะดึก',
      shift: 'Night',
      pointsInspected: 9,
      submitted: 150,
      submittedInspected: 40,
      notInspected: 110,
      inspections: 41,
      reworks: 2,
      gpsFlagged: 1,
    }),
    summaryRow({
      supervisorId: 33,
      employeeId: '660003',
      displayName: 'หัวหน้าตึกบี',
      buildingCode: 'B',
      pointsInspected: 0,
      pointsTotal: 10,
      submitted: 0,
      submittedInspected: 0,
      notInspected: 0,
      inspections: 0,
      reworks: 0,
      gpsFlagged: 0,
    }),
  ],
};
