import { statusOf } from '../../shared/api/scanAnswers';
import type { PointCard } from '../../shared/api/types';
import type { GpsFix, LocationProblem } from '../../shared/geo/readLocation';
import { formatRound, formatTime } from '../../shared/utils/thaiTime';
import type { InspectionResult, InspectionSubmission, RecordInspectionRequest } from './inspectApi';

/** The verdict being made (facility-0024: a draft lives in useState). Nothing is chosen at first (plan 9 decision 4). */
export interface InspectDraft {
  result: InspectionResult | null;
  defect: string;
}

export const emptyDraft: InspectDraft = { result: null, defect: '' };

/** inspections.defect is VARCHAR(1000) (InspectionRules.MaxDefectLength). */
export const MAX_DEFECT_LENGTH = 1000;

/** Why บันทึกผลตรวจ cannot go yet; the server would refuse both (400). */
export function draftProblem(draft: InspectDraft): string | null {
  if (draft.result === null) {
    return 'เลือกผลการตรวจก่อน';
  }
  return draft.result === 'Rework' && draft.defect.trim() === '' ? 'ใส่ข้อบกพร่องก่อนบันทึก' : null;
}

export function describeDraft(draft: InspectDraft): string {
  return draft.result === 'Rework' ? `ไม่สะอาด: ${draft.defect.trim()}` : 'สะอาด';
}

/** The defect goes only with ไม่สะอาด: the server refuses one with สะอาด. */
export function requestOf(qrToken: string, scanRecordId: number, draft: InspectDraft, fix: GpsFix): RecordInspectionRequest {
  if (draft.result === 'Rework') {
    return { qrToken, scanRecordId, result: 'Rework', defect: draft.defect.trim(), ...fix };
  }
  return { qrToken, scanRecordId, result: 'Passed', ...fix };
}

/** M6 "ส่งงานล่าสุด": when, who, which round, what the Cleaner reported, and how the server placed it. */
export function submissionLine(submission: InspectionSubmission, point: PointCard): string {
  const reported = [...submission.issueTags, submission.note].filter(Boolean);
  const status =
    submission.status === 'Normal' ? 'เรียบร้อย' : reported.length > 0 ? `พบปัญหา: ${reported.join(' · ')}` : 'พบปัญหา';
  const placement = submission.placement === 'Late' ? 'ส่งช้า' : submission.placement === 'Rework' ? 'งานแก้' : null;
  return [
    formatTime(submission.submittedAt),
    submission.cleanerName,
    point.currentRound ? `รอบ ${formatRound(point.currentRound)}` : null,
    status,
    placement,
  ]
    .filter(Boolean)
    .join(' · ');
}

/** The open's own error for a location problem (inspectApi openInspection): a CUSTOM_ERROR naming it. */
export function locationProblemOf(error: unknown): LocationProblem | null {
  return statusOf(error) === 'CUSTOM_ERROR' ? (error as { error: LocationProblem }).error : null;
}
