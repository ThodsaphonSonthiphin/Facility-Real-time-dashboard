import type { InspectionSummary, SummaryRow } from '../pages/inspection-summary/summaryApi';
import type { AuditActor, AuditEntry, AuditPage } from '../pages/history/historyApi';

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
    uninspectedPoints: [],
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
      uninspectedPoints: [
        { pointId: 1, areaCode: 'AR01', pointName: 'ห้องน้ำทดสอบ', signCode: 'AR01-01' },
        { pointId: 2, areaCode: 'AR01', pointName: 'ทางเดินทดสอบ', signCode: 'AR01-02' },
        { pointId: 3, areaCode: 'AR02', pointName: 'ห้องประชุมทดสอบ', signCode: 'AR02-01' },
      ],
    }),
    summaryRow({
      supervisorId: 33,
      employeeId: '660003',
      displayName: 'หัวหน้าตึกบี',
      buildingCode: 'B',
      pointsInspected: 0,
      pointsTotal: 10,
      uninspectedPoints: Array.from({ length: 10 }, (_, index) => ({
        pointId: 10 + index,
        areaCode: 'BR01',
        pointName: `จุดทดสอบ ${index + 1}`,
        signCode: `BR01-${String(index + 1).padStart(2, '0')}`,
      })),
      submitted: 0,
      submittedInspected: 0,
      notInspected: 0,
      inspections: 0,
      reworks: 0,
      gpsFlagged: 0,
    }),
  ],
};

/** The log's actors, by name, as the server orders them. Account 7 is the logged-in Admin. */
export const actors: AuditActor[] = [
  { id: 8, displayName: 'ผู้ดูแลคนที่สอง' },
  { id: 7, displayName: 'ผู้ดูแลทดสอบ' },
];

/** A Shift-In the Admin added at 10:42 on 15/10 for 06:58, with the reason. Snapshots keep null fields, as stored. */
export function auditEntry(overrides: Partial<AuditEntry> = {}): AuditEntry {
  return {
    id: 50,
    occurredAt: '2026-10-15T03:42:00Z', // 10:42
    actorId: 7,
    actorName: 'ผู้ดูแลทดสอบ',
    action: 'ATTENDANCE_ADD',
    category: 'Attendance',
    entityType: 'shift_attendances',
    entityId: 900,
    summary: 'เพิ่มเวลาเข้างานของ 650001 แม่บ้านทดสอบ AR01 กะเช้า 15/10/2026',
    after: { EventType: 'ShiftIn', OccurredAt: '2026-10-14T23:58:00Z', Source: 'AdminAdd' }, // 06:58
    reason: 'มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน',
    ...overrides,
  };
}

/** The newest page: five rows, then nextBeforeId for the older one. */
export const firstPage: AuditPage = {
  entries: [
    auditEntry(),
    auditEntry({
      id: 49,
      occurredAt: '2026-10-15T02:12:00Z', // 09:12
      actorId: 8,
      actorName: 'ผู้ดูแลคนที่สอง',
      action: 'COVER_ASSIGN',
      category: 'Cover',
      entityType: 'cover_assignments',
      entityId: 31,
      summary: 'มอบหมาย แม่บ้านที่อื่น ทำแทน AR01 กะเช้า 15/10/2026',
      after: { UserId: 25, AreaId: 1, BuildingId: null, ShiftDate: '2026-10-15', Shift: 'Day', BlockedScanId: null },
      reason: undefined,
    }),
    auditEntry({
      id: 48,
      occurredAt: '2026-10-15T02:05:00Z', // 09:05
      action: 'ALERT_REVIEW',
      category: 'AlertReview',
      entityType: 'shift_attendances',
      entityId: 401,
      summary: 'ปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ เวลา 06:52',
      before: { Reviewed: false },
      after: { Reviewed: true, AlertType: 'AttendanceGps' },
      reason: 'GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง',
    }),
    auditEntry({
      id: 47,
      occurredAt: '2026-10-14T04:20:00Z', // 14/10 11:20
      action: 'ACCOUNT_PHONE',
      category: 'Account',
      entityType: 'users',
      entityId: 21,
      summary: 'เปลี่ยนเบอร์โทรของ 650001 แม่บ้านทดสอบ',
      after: undefined,
      reason: undefined,
    }),
    auditEntry({
      id: 46,
      occurredAt: '2026-10-14T01:15:00Z', // 14/10 08:15
      action: 'EXPORT_EXCEL',
      category: 'Export',
      entityType: 'export',
      entityId: undefined,
      summary: 'ส่งออก Excel การลงเวลา + การส่งงาน วันที่ 2026-10-01 ถึง 2026-10-05 ทุกตึก',
      after: { From: '2026-10-01', To: '2026-10-05', BuildingId: null, Sets: ['Attendance', 'Submissions'], Rows: [28, 412] },
      reason: undefined,
    }),
  ],
  nextBeforeId: 46,
};

/** The older page (beforeId=46): the last one. */
export const olderPage: AuditPage = {
  entries: [
    auditEntry({
      id: 45,
      occurredAt: '2026-10-13T07:40:00Z', // 13/10 14:40
      action: 'QR_REGENERATE',
      category: 'QrToken',
      entityType: 'signs',
      entityId: 103,
      summary: 'ออก QR ใหม่ของป้าย AR01-03 ป้ายเดิมสแกนไม่ได้แล้ว',
      before: { QrIssuedAt: '2026-10-01T02:00:00Z' },
      after: { QrIssuedAt: '2026-10-13T07:40:00Z' },
      reason: undefined,
    }),
  ],
};
