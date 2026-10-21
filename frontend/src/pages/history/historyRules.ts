import { readRange, toRangeParams } from '../../shared/utils/dateRange';
import type { AuditCategory, HistoryArgs } from './historyApi';

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
