import type { AreaDetail, CleanerOption, ShiftPattern } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';
import type { AreaFormBody } from './areaFormApi';

/** The form as typed: selects hold ids as strings, '' for none. */
export interface AreaDraft {
  code: string;
  name: string;
  buildingId: string;
  shiftPattern: ShiftPattern;
  dayCleanerId: string;
  nightCleanerId: string;
}

/** Empty for "+ เพิ่ม Area", the Area's own values for "แก้ไข Area" (D10). */
export function draftOf(detail?: AreaDetail): AreaDraft {
  const area = detail?.area;
  return {
    code: '',
    name: area?.name ?? '',
    buildingId: area ? String(area.buildingId) : '',
    shiftPattern: area?.shiftPattern ?? 'DayAndNight',
    dayCleanerId: area?.dayCleaner ? String(area.dayCleaner.id) : '',
    nightCleanerId: area?.nightCleaner ? String(area.nightCleaner.id) : '',
  };
}

/** D10: "รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มี Area ในกะนั้น", plus this Area's own Cleaner when editing. */
export function cleanerChoices(cleaners: CleanerOption[], shift: Shift, areaId: number | undefined): CleanerOption[] {
  return cleaners.filter((cleaner) => cleaner.shift === shift && (cleaner.areaId == null || cleaner.areaId === areaId));
}

/** What the page checks before sending; the server checks the rest (code format, slots) and its message is shown. */
export function draftProblem(draft: AreaDraft, isNew: boolean): string | null {
  if (isNew && draft.code.trim() === '') {
    return 'ใส่รหัสพื้นที่ก่อน';
  }
  if (draft.name.trim() === '') {
    return 'ใส่ชื่อพื้นที่ก่อน';
  }
  if (draft.buildingId === '') {
    return 'เลือกตึกก่อน';
  }
  return null;
}

/** A day-only Area has no night Cleaner (the server refuses one). */
export function formBody(draft: AreaDraft): AreaFormBody {
  return {
    name: draft.name.trim(),
    buildingId: Number(draft.buildingId),
    shiftPattern: draft.shiftPattern,
    dayCleanerId: draft.dayCleanerId ? Number(draft.dayCleanerId) : null,
    nightCleanerId: draft.shiftPattern === 'DayOnly' || !draft.nightCleanerId ? null : Number(draft.nightCleanerId),
  };
}
