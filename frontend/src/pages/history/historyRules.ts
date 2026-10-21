import { readRange, toRangeParams } from '../../shared/utils/dateRange';
import { eventLabel, shiftPatternLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatDayTime, formatShiftDate } from '../../shared/utils/thaiTime';
import type { AuditCategory, AuditEntry, HistoryArgs } from './historyApi';

/** AdminAuditLogEndpoints: maxDays 92. */
export const HISTORY_MAX_DAYS = 92;

export const auditCategories: readonly AuditCategory[] = [
  'Attendance',
  'Cover',
  'AlertReview',
  'Setup',
  'QrToken',
  'Account',
  'Export',
];

/** Wireframe D9's "ประเภท" options, word for word. */
export const categoryLabel: Record<AuditCategory, string> = {
  Attendance: 'แก้การลงเวลา',
  Cover: 'มอบหมายทำแทน',
  AlertReview: 'ปิดเตือน',
  Setup: 'Area · จุด · รอบ · ป้าย',
  QrToken: 'ออก QR ใหม่',
  Account: 'บัญชี',
  Export: 'ส่งออก Excel',
};

/** Each action's Thai name for the "ประเภท" column (plan 12 decision 8); AuditCategories.cs lists the actions. */
export const actionLabel: Record<string, string> = {
  ATTENDANCE_ADD: 'เพิ่มเวลาลงงาน',
  ATTENDANCE_EDIT: 'แก้เวลาลงงาน',
  COVER_ASSIGN: 'มอบหมายทำแทน',
  COVER_CANCEL: 'ยกเลิกทำแทน',
  ALERT_REVIEW: 'ปิดเตือน',
  AREA_CREATE: 'เพิ่ม Area',
  AREA_UPDATE: 'แก้ Area',
  AREA_ACTIVATE: 'เปิดใช้ Area',
  AREA_DEACTIVATE: 'ปิดใช้ Area',
  POINT_CREATE: 'เพิ่มจุด',
  POINT_UPDATE: 'แก้จุดและช่วงรอบ',
  POINT_ACTIVATE: 'เปิดใช้จุด',
  POINT_DEACTIVATE: 'ปิดใช้จุด',
  SIGN_LOCATION: 'เก็บพิกัดป้าย',
  SIGN_RADIUS: 'แก้รัศมีป้าย',
  QR_REGENERATE: 'ออก QR ใหม่',
  ACCOUNT_CREATE: 'เพิ่มบัญชี',
  ACCOUNT_UPDATE: 'แก้บัญชี',
  ACCOUNT_PHONE: 'แก้เบอร์โทร',
  ACCOUNT_PASSWORD: 'รีเซ็ตรหัสผ่าน',
  ACCOUNT_ACTIVATE: 'เปิดใช้บัญชี',
  ACCOUNT_DEACTIVATE: 'ปิดใช้บัญชี',
  EXPORT_EXCEL: 'ส่งออก Excel',
};

/** An action no label lists yet shows as stored, so nothing is hidden. */
export function actionText(action: string): string {
  return Object.hasOwn(actionLabel, action) ? actionLabel[action] : action;
}

/** ?from&to&actor=<id>&category=<AuditCategory>; anything else is left out, because an empty filter gets 400. */
export function readHistoryArgs(params: URLSearchParams): HistoryArgs {
  const args: HistoryArgs = { ...readRange(params) };
  const actor = Number(params.get('actor'));
  if (Number.isInteger(actor) && actor > 0) {
    args.actorId = actor;
  }
  const category = auditCategories.find((each) => each === params.get('category'));
  if (category) {
    args.category = category;
  }
  return args;
}

export function toHistoryParams({ from, to, actorId, category }: HistoryArgs): URLSearchParams {
  const params = toRangeParams(from && to ? { from, to } : undefined, undefined);
  if (actorId) {
    params.set('actor', String(actorId));
  }
  if (category) {
    params.set('category', category);
  }
  return params;
}

/** facility-0057: these actions store nothing about the secret; the line says what changed without it. */
const secretChange: Record<string, string> = {
  ACCOUNT_PHONE: 'เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)',
  ACCOUNT_PASSWORD: 'รหัสผ่านเดิม → รหัสผ่านใหม่ (ไม่แสดงรหัสผ่าน)',
};

/** Snapshot fields in Thai (the AuditTrail.Add calls). Ids stay numbers: snapshots keep ids, not names, on purpose. */
const fieldLabel: Record<string, string> = {
  EventType: 'รายการ',
  OccurredAt: 'เวลา',
  Source: 'ที่มา',
  Reviewed: 'คุยแล้ว',
  AlertType: 'ประเภทเตือน',
  IsActive: 'เปิดใช้งาน',
  Name: 'ชื่อ',
  RoundWindows: 'ช่วงรอบ',
  RadiusM: 'รัศมี (ม.)',
  QrIssuedAt: 'ออก QR',
  Latitude: 'ละติจูด',
  Longitude: 'ลองจิจูด',
  LocationAccuracyM: 'ความแม่นยำ (ม.)',
  LocationSource: 'ที่มาของพิกัด',
  Role: 'บทบาท',
  LoginName: 'ชื่อเข้าระบบ',
  DisplayName: 'ชื่อที่แสดง',
  AreaId: 'Area (รหัสในระบบ)',
  BuildingId: 'ตึก (รหัสในระบบ)',
  Shift: 'กะ',
  Code: 'รหัส',
  ShiftPattern: 'กะของ Area',
  DayCleanerId: 'แม่บ้านกะเช้า (รหัสในระบบ)',
  NightCleanerId: 'แม่บ้านกะดึก (รหัสในระบบ)',
  RemovedNightWindows: 'รอบกะดึกที่ถูกลบ',
  UserId: 'คนทำแทน (รหัสในระบบ)',
  ShiftDate: 'วันที่ของกะ',
  BlockedScanId: 'สแกนที่ถูกบล็อก (รหัสในระบบ)',
  cancelled: 'ยกเลิกแล้ว',
  alsoCancelled: 'ยกเลิกพร้อมกัน (รหัสในระบบ)',
  From: 'ตั้งแต่',
  To: 'ถึง',
  Sets: 'ชุดข้อมูล',
  Rows: 'จำนวนแถว',
};

/** Enum values in Thai, by field. */
const valueLabel: Record<string, Record<string, string>> = {
  EventType: eventLabel,
  Source: { Scan: 'สแกนเอง', AdminAdd: 'Admin เพิ่ม', AdminEdit: 'แก้โดย Admin' },
  Shift: shiftShortLabel,
  Role: { Cleaner: 'แม่บ้าน', Supervisor: 'หัวหน้า', Admin: 'Admin' },
  LocationSource: { Site: 'หน้างาน', Map: 'แผนที่' },
  ShiftPattern: shiftPatternLabel,
  AlertType: {
    AttendanceGps: 'ลงเวลาไกลจากป้าย',
    SubmissionGps: 'ส่งงานไกลจากป้าย',
    BlockedScan: 'สแกนที่ถูกบล็อก',
    InspectionGps: 'ตรวจงานไกลจากป้าย',
  },
  Sets: { Attendance: 'การลงเวลา', Submissions: 'การส่งงาน', Inspections: 'การตรวจงาน' },
};

/** AREA_UPDATE always stores how many night windows went; 0 is "none", like the null before it. */
const ZERO_IS_NONE = new Set(['RemovedNightWindows']);

/** Format by field, never by shape: Admin-typed text (names, login names) is shown exactly as stored. */
const MOMENT_FIELDS = new Set(['OccurredAt', 'QrIssuedAt']);
const DATE_FIELDS = new Set(['ShiftDate', 'From', 'To']);

function valueText(field: string, value: unknown): string {
  if (value == null) {
    return '–';
  }
  if (Array.isArray(value)) {
    return value.length === 0 ? '–' : value.map((item) => valueText(field, item)).join(', ');
  }
  if (typeof value === 'boolean') {
    return value ? 'ใช่' : 'ไม่ใช่';
  }
  if (typeof value === 'number') {
    return value === 0 && ZERO_IS_NONE.has(field) ? '–' : String(value);
  }
  if (typeof value === 'string') {
    const labels = valueLabel[field];
    if (labels && Object.hasOwn(labels, value)) {
      return labels[value];
    }
    if (field === 'RoundWindows') {
      return value.replace(/^DAY /, 'เช้า ').replace(/^NIGHT /, 'ดึก ');
    }
    if (Number.isNaN(Date.parse(value))) {
      return value;
    }
    if (MOMENT_FIELDS.has(field)) {
      return formatDayTime(value);
    }
    return DATE_FIELDS.has(field) ? formatShiftDate(value) : value;
  }
  return JSON.stringify(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Wireframe D9's "ก่อน → หลัง" (plan 12 decision 8): one "label: before → after" line per field that shows
 * differently on the two sides, in the order the snapshots list them. An unknown field shows its key.
 */
export function changeLines({ action, before, after }: Pick<AuditEntry, 'action' | 'before' | 'after'>): string[] {
  if (Object.hasOwn(secretChange, action)) {
    return [secretChange[action]];
  }
  if (before == null && after == null) {
    return [];
  }
  const was = before ?? {};
  const now = after ?? {};
  if (!isRecord(was) || !isRecord(now)) {
    return [`${valueText('', before)} → ${valueText('', after)}`];
  }
  const fields = [...new Set([...Object.keys(was), ...Object.keys(now)])];
  return fields
    .map((field) => ({ field, from: valueText(field, was[field]), to: valueText(field, now[field]) }))
    .filter((change) => change.from !== change.to)
    .map((change) => `${Object.hasOwn(fieldLabel, change.field) ? fieldLabel[change.field] : change.field}: ${change.from} → ${change.to}`);
}

/** The search box (plan 12 decision 7): the rows already loaded whose summary, actor, reason or type matches. */
export function searchEntries(entries: readonly AuditEntry[], search: string): AuditEntry[] {
  const text = search.trim().toLowerCase();
  if (text === '') {
    return [...entries];
  }
  return entries.filter((entry) =>
    [entry.summary, entry.actorName, entry.reason, actionText(entry.action)].some((value) =>
      value?.toLowerCase().includes(text),
    ),
  );
}
