import { formatShiftDate, parseShiftDate } from './thaiTime';

/**
 * The date ranges of the Admin's report pages (D7, D8, D9). Dates are Thai shift dates "yyyy-MM-dd", both ends
 * included, as the report endpoints take them (from, to).
 */
export interface DateRange {
  from: string;
  to: string;
}

/** What the two typed fields hold, dd/mm/yyyy (plan 11 decision 13). */
export interface RangeTexts {
  fromText: string;
  toText: string;
}

/** Wireframe D8's shortcuts. */
export type RangePreset = 'today' | 'week' | 'month';

export const rangePresets: readonly RangePreset[] = ['today', 'week', 'month'];

export const presetLabel: Record<RangePreset, string> = { today: 'วันนี้', week: '7 วัน', month: 'เดือนนี้' };

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const DAY_MS = 86_400_000;

/** A "yyyy-MM-dd" date moved by whole days. */
export function addDays(date: string, days: number): string {
  return new Date(Date.parse(`${date}T00:00:00Z`) + days * DAY_MS).toISOString().slice(0, 10);
}

/** How many dates a range covers, both ends included. */
export function rangeDays({ from, to }: DateRange): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / DAY_MS) + 1;
}

/** The server's default range on D7 and D9: the 7 dates ending today. */
export function lastSevenDays(today: string): DateRange {
  return { from: addDays(today, -6), to: today };
}

export function presetRange(preset: RangePreset, today: string): DateRange {
  if (preset === 'today') {
    return { from: today, to: today };
  }
  if (preset === 'week') {
    return lastSevenDays(today);
  }
  return { from: `${today.slice(0, 8)}01`, to: today };
}

export function rangeTexts({ from, to }: DateRange): RangeTexts {
  return { fromText: formatShiftDate(from), toText: formatShiftDate(to) };
}

/** Whether the typed fields hold exactly this shortcut's range today (RangeFields marks that button). */
export function matchesPreset(texts: RangeTexts, preset: RangePreset, today: string): boolean {
  const expected = rangeTexts(presetRange(preset, today));
  return texts.fromText.trim() === expected.fromText && texts.toText.trim() === expected.toText;
}

/**
 * The typed range, checked before asking (facility-0022 item 4), or the message to show. The server makes the same
 * checks (31 dates for the summary and the export, 92 for the history), and its message is shown if it still refuses.
 */
export function checkRange({ fromText, toText }: RangeTexts, maxDays: number): DateRange | string {
  const from = parseShiftDate(fromText);
  const to = parseShiftDate(toText);
  if (!from || !to) {
    return 'วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026';
  }
  if (to < from) {
    return 'วันสุดท้ายต้องไม่ก่อนวันเริ่ม';
  }
  return rangeDays({ from, to }) > maxDays ? `เลือกได้ไม่เกิน ${maxDays} วัน` : { from, to };
}

/** ?from=yyyy-MM-dd&to=yyyy-MM-dd: both or neither, because the server refuses one alone. */
export function readRange(params: URLSearchParams): DateRange | undefined {
  const from = params.get('from');
  const to = params.get('to');
  return from && to && ISO_DATE.test(from) && ISO_DATE.test(to) ? { from, to } : undefined;
}

/** ?building=<id>. */
export function readBuildingId(params: URLSearchParams): number | undefined {
  const building = Number(params.get('building'));
  return Number.isInteger(building) && building > 0 ? building : undefined;
}

/** The address for a range and a building, in the words readRange and readBuildingId read. */
export function toRangeParams(range: DateRange | undefined, buildingId: number | undefined): URLSearchParams {
  const params = new URLSearchParams();
  if (range) {
    params.set('from', range.from);
    params.set('to', range.to);
  }
  if (buildingId) {
    params.set('building', String(buildingId));
  }
  return params;
}

/** The export page (D8) for this range and building: the "ส่งออก Excel" links of D2 and D7 (plan 12 decision 6). */
export function exportLink(range: DateRange | undefined, buildingId: number | undefined): string {
  const query = toRangeParams(range, buildingId).toString();
  return query ? `/admin/export?${query}` : '/admin/export';
}
