import type { AttendanceState, CleaningStatus } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';
import type { SubmitScanRequest } from './scanApi';

/** The form's choices (facility-0024: a draft lives in useState). They stay through a failed send (facility-0052). */
export interface ScanDraft {
  status: CleaningStatus;
  tags: string[];
  note: string;
}

/** facility-0007: เรียบร้อย is already chosen, so a clean point is two taps. */
export const emptyDraft: ScanDraft = { status: 'Normal', tags: [], note: '' };

/** Wireframe M4's quick tags, stored as the words the Cleaner sees (facility-0046). */
export const issueTags: readonly string[] = ['ขยะล้น', 'น้ำยาหมด', 'พื้นเปียก', 'ของชำรุด'];

/** พบปัญหา needs something to report: a tag or a note (plan 8 decision 7). */
export function isComplete(draft: ScanDraft): boolean {
  return draft.status === 'Normal' || draft.tags.length > 0 || draft.note.trim() !== '';
}

export function describeDraft(draft: ScanDraft): string {
  if (draft.status === 'Normal') {
    return 'เรียบร้อย';
  }
  return `พบปัญหา: ${[...draft.tags, draft.note.trim()].filter((part) => part !== '').join(' · ')}`;
}

/** Tags and the note go only with พบปัญหา. */
export function requestOf(qrToken: string, draft: ScanDraft, fix: GpsFix): SubmitScanRequest {
  if (draft.status === 'Normal') {
    return { qrToken, status: 'Normal', ...fix };
  }
  const note = draft.note.trim();
  return { qrToken, status: 'Issue', issueTags: draft.tags, ...(note === '' ? {} : { notes: note }), ...fix };
}

export type OffDutyReason = 'outside-shift' | 'not-checked-in' | 'checked-out';

/**
 * The server's 409s, asked before the form (plan 8 decision 4): a Cleaner submits from Shift-In until Shift-Out in
 * their own shift's window; a break does not stop it (AttendanceRules.IsOnDuty, facility-0069).
 */
export function offDutyReason(attendance: AttendanceState): OffDutyReason | null {
  if (attendance.shift === null) {
    return 'outside-shift';
  }
  const recorded = attendance.events.map((entry) => entry.eventType);
  if (!recorded.includes('ShiftIn')) {
    return 'not-checked-in';
  }
  return recorded.includes('ShiftOut') ? 'checked-out' : null;
}
