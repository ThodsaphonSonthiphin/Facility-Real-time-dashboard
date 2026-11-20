import type { Shift } from '../api/types';
import type { ContextResult, OverviewGroup, OverviewSelection, OverviewView } from './types';

export const OVERVIEW_GROUPS: readonly OverviewGroup[] = [
  'entered',
  'covered',
  'unassigned',
  'notEntered',
  'submitted',
  'incomplete',
  'inspected',
  'pendingInspection',
  'noSubmission',
  'rework',
  'issue',
  'overdue',
  'upcoming',
  'noRounds',
  'missedCleaning',
  'missedInspection',
];

const OVERVIEW_GROUP_SET = new Set<string>(OVERVIEW_GROUPS);
const OVERVIEW_VIEW_SET = new Set<string>(['attendance', 'cleaning', 'inspection']);
const KNOWN_KEYS = ['date', 'shift', 'building', 'group', 'page', 'areaId', 'view', 'roundKey', 'source'] as const;
const FORBIDDEN_REDIRECT_KEYS = new Set(['returnurl', 'redirect', 'redirecturl', 'returnto', 'redirectto']);
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const POSITIVE_INT = /^[1-9]\d*$/;

function isRealIsoDate(value: string): boolean {
  if (!ISO_DATE.test(value)) {
    return false;
  }
  const timestamp = Date.parse(`${value}T00:00:00Z`);
  return !Number.isNaN(timestamp) && new Date(timestamp).toISOString().slice(0, 10) === value;
}

function parsePositiveInt(value: string): number | null {
  if (!POSITIVE_INT.test(value)) {
    return null;
  }
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function isShift(value: string): value is Shift {
  return value === 'Day' || value === 'Night';
}

function isOverviewGroup(value: string): value is OverviewGroup {
  return OVERVIEW_GROUP_SET.has(value);
}

function isOverviewView(value: string): value is OverviewView {
  return OVERVIEW_VIEW_SET.has(value);
}

function fail(message: string): ContextResult {
  return { ok: false, message };
}

/**
 * Parses and strictly validates overview URL parameters.
 * Rejects partial date/shift pairs, invalid calendar dates, unknown groups, non-positive/unsafe integer IDs or page,
 * duplicate known keys, and arbitrary redirect parameters. When `destination` is true, requires date/shift/areaId/view.
 */
export function readOverviewContext(params: URLSearchParams, destination = false): ContextResult {
  for (const key of params.keys()) {
    if (FORBIDDEN_REDIRECT_KEYS.has(key.toLowerCase())) {
      return fail('ลิงก์ไม่ถูกต้อง');
    }
  }

  for (const key of KNOWN_KEYS) {
    if (params.getAll(key).length > 1) {
      return fail('ลิงก์ไม่ถูกต้อง');
    }
  }

  if (params.has('source') && params.get('source') !== 'overview') {
    return fail('ลิงก์ไม่ถูกต้อง');
  }

  const hasDate = params.has('date');
  const hasShift = params.has('shift');
  if (hasDate !== hasShift) {
    return fail('ต้องระบุวันที่และกะคู่กัน');
  }

  let date: string | undefined;
  let shift: Shift | undefined;
  if (hasDate && hasShift) {
    const rawDate = params.get('date')!;
    const rawShift = params.get('shift')!;
    if (!isRealIsoDate(rawDate) || !isShift(rawShift)) {
      return fail('วันที่หรือกะไม่ถูกต้อง');
    }
    date = rawDate;
    shift = rawShift;
  }

  let building: number | undefined;
  if (params.has('building')) {
    const parsedBuilding = parsePositiveInt(params.get('building')!);
    if (parsedBuilding === null) {
      return fail('รหัสตึกไม่ถูกต้อง');
    }
    building = parsedBuilding;
  }

  let group: OverviewGroup = 'notEntered';
  if (params.has('group')) {
    const rawGroup = params.get('group')!;
    if (!isOverviewGroup(rawGroup)) {
      return fail('หมวดหมู่ไม่ถูกต้อง');
    }
    group = rawGroup;
  }

  let page = 1;
  if (params.has('page')) {
    const parsedPage = parsePositiveInt(params.get('page')!);
    if (parsedPage === null) {
      return fail('เลขหน้าไม่ถูกต้อง');
    }
    page = parsedPage;
  }

  let areaId: number | undefined;
  if (params.has('areaId')) {
    const parsedAreaId = parsePositiveInt(params.get('areaId')!);
    if (parsedAreaId === null) {
      return fail('รหัสพื้นที่ไม่ถูกต้อง');
    }
    areaId = parsedAreaId;
  }

  let view: OverviewView | undefined;
  if (params.has('view')) {
    const rawView = params.get('view')!;
    if (!isOverviewView(rawView)) {
      return fail('มุมมองไม่ถูกต้อง');
    }
    view = rawView;
  }

  let roundKey: string | undefined;
  if (params.has('roundKey')) {
    const rawRoundKey = params.get('roundKey')!;
    if (rawRoundKey.trim().length === 0) {
      return fail('รหัสรอบไม่ถูกต้อง');
    }
    roundKey = rawRoundKey;
  }

  if (destination && (!date || !shift || areaId === undefined || view === undefined)) {
    return fail('ข้อมูลลิงก์ไม่ครบ');
  }

  const context: OverviewSelection = { group, page };
  if (date !== undefined && shift !== undefined) {
    context.date = date;
    context.shift = shift;
  }
  if (building !== undefined) {
    context.building = building;
  }
  if (areaId !== undefined) {
    context.areaId = areaId;
  }
  if (view !== undefined) {
    context.view = view;
  }
  if (roundKey !== undefined) {
    context.roundKey = roundKey;
  }

  return { ok: true, context };
}

/** Emits date/shift together, optional building, group and page, then optional areaId/view/roundKey. */
export function overviewParams(context: OverviewSelection): URLSearchParams {
  const params = new URLSearchParams();
  if (context.date && context.shift) {
    params.set('date', context.date);
    params.set('shift', context.shift);
  }
  if (context.building !== undefined) {
    params.set('building', String(context.building));
  }
  params.set('group', context.group);
  params.set('page', String(context.page));
  if (context.areaId !== undefined) {
    params.set('areaId', String(context.areaId));
  }
  if (context.view !== undefined) {
    params.set('view', context.view);
  }
  if (context.roundKey !== undefined) {
    params.set('roundKey', context.roundKey);
  }
  return params;
}

/** Canonical overview URL preserving slot, building, group, and page while stripping destination-only keys. */
export function overviewHref(context: OverviewSelection): string {
  const params = overviewParams(context);
  params.delete('source');
  params.delete('areaId');
  params.delete('view');
  params.delete('roundKey');
  return `/admin/overview?${params.toString()}`;
}

/** Drill-through URL to the scoped Attendance or Dashboard Area view with source=overview. */
export function areaHref(
  context: OverviewSelection,
  areaId: number,
  view: OverviewView,
  roundKey?: string,
): string {
  const params = overviewParams({ ...context, areaId, view, roundKey });
  params.set('source', 'overview');
  const path = view === 'attendance' ? '/admin/attendance' : '/dashboard';
  return `${path}?${params.toString()}`;
}
