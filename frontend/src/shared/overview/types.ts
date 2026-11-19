import type { AttendanceEvent, AttendanceSource, CleaningStatus, Placement, Shift } from '../api/types';

/** Fixed group keys accepted by GET /api/admin/overview/areas and summary group counts. */
export type OverviewGroup =
  | 'entered'
  | 'covered'
  | 'unassigned'
  | 'notEntered'
  | 'submitted'
  | 'incomplete'
  | 'inspected'
  | 'pendingInspection'
  | 'noSubmission'
  | 'rework'
  | 'issue'
  | 'overdue'
  | 'upcoming'
  | 'noRounds'
  | 'missedCleaning'
  | 'missedInspection';

/** Destination drill-through mode when leaving the overview for an Area. */
export type OverviewView = 'attendance' | 'cleaning' | 'inspection';

/** Parsed or selected overview URL context shared across overview, attendance, and dashboard pages. */
export interface OverviewSelection {
  date?: string;
  shift?: Shift;
  building?: number;
  group: OverviewGroup;
  page: number;
  areaId?: number;
  view?: OverviewView;
  roundKey?: string;
}

export type ContextResult = { ok: true; context: OverviewSelection } | { ok: false; message: string };

export interface OverviewArgs {
  shiftDate?: string;
  shift?: Shift;
  buildingId?: number;
}

export interface OverviewListArgs extends OverviewArgs {
  group: OverviewGroup;
  page: number;
  pageSize: 5;
}

export interface OverviewDetailArgs extends OverviewArgs {
  areaId: number;
  group: OverviewGroup;
  roundKey?: string;
}

export interface Availability {
  state: 'complete' | 'partial' | 'unavailable' | 'notStarted';
  reasons: string[];
}

export interface OverviewCount {
  value: number | null;
  observed: number;
  availability: Availability;
}

export interface SectionAvailability {
  entry: Availability;
  work: Availability;
  inspection: Availability;
  history: Availability;
  issue: Availability;
}

export interface OverviewRange {
  completeFrom?: string | null;
  completeThrough?: string | null;
  captureStartedAtUtc?: string | null;
}

export interface OverviewContext {
  shiftDate: string;
  shift: Shift;
  buildingId?: number | null;
  phase: 'running' | 'receiving' | 'closed' | 'notStarted';
  isClockCurrent: boolean;
  evaluationUtc: string;
  readAtUtc: string;
  closeUtc?: string | null;
  targetRevisionId?: number | null;
  range: OverviewRange;
}

export interface OverviewMetric {
  numerator: OverviewCount;
  denominator: OverviewCount;
}

export interface OverviewBuilding {
  id: number;
  code: string;
  name: string;
  entry: OverviewMetric;
  submission: OverviewMetric;
  inspection: OverviewMetric;
  groups: Record<OverviewGroup, OverviewCount>;
}

export interface OverviewSummary {
  context: OverviewContext;
  availability: SectionAvailability;
  entry: OverviewMetric;
  submission: OverviewMetric;
  inspection: OverviewMetric;
  groups: Record<OverviewGroup, OverviewCount>;
  buildings: OverviewBuilding[];
}

export interface AreaWork {
  duePoints: number;
  submittedPoints: number;
  inspectedPoints: number;
  submitted: boolean;
  inspected: boolean;
  incomplete: boolean;
  pendingInspection: boolean;
  noSubmission: boolean;
  rework: boolean;
  issue: boolean;
  overdue: boolean;
}

export interface OverviewPoint {
  pointId: number;
  isDue: boolean;
  isSubmitted: boolean;
  isInspected: boolean;
  needsRework: boolean;
  isIssue: boolean;
  isOverdue: boolean;
  isUpcoming: boolean;
  submissionId?: number | null;
  inspectionId?: number | null;
}

export interface OverviewPointDetail {
  work: OverviewPoint;
  code?: string | null;
  name: string;
  sortOrder: number;
  roundKey?: string | null;
  start?: string | null;
  end?: string | null;
}

export interface OverviewEntryEvidence {
  areaId: number;
  shiftDate: string;
  shift: Shift;
  kind: 'shift-in' | 'transfer-arrival';
  recordedAt: string;
  transferId?: number | null;
}

export interface OverviewResponsibility {
  userId: number;
  areaId: number;
  state: 'instructed' | 'confirmed';
  lastConfirmedAreaId?: number | null;
  confirmedAt?: string | null;
}

export interface OverviewAreaRow {
  areaId: number;
  buildingId: number;
  code: string;
  name: string;
  buildingCode: string;
  buildingName: string;
  entry: 'entered' | 'covered' | 'unassigned';
  work: AreaWork;
  reasons: string[];
  roundKeys: string[];
  originsIncomplete?: boolean;
}

export interface OverviewAreaList {
  summary: OverviewSummary;
  group: OverviewGroup;
  page: number;
  pageSize: number;
  total: OverviewCount;
  rows: OverviewAreaRow[];
}

export interface OverviewSubmission {
  id: number;
  pointId: number;
  shiftDate: string;
  shift: Shift;
  originId?: number | null;
  existingWindowId?: number | null;
  roundStart?: string | null;
  roundEnd?: string | null;
  placement: Placement;
  status: CleaningStatus;
  submittedAt: string;
  userId: number;
  issueTags?: string | null;
  note?: string | null;
}

export interface OverviewInspection {
  id: number;
  submissionId: number;
  result: 'Passed' | 'Rework';
  inspectedAt: string;
  supervisorId: number;
  defect?: string | null;
}

export interface OverviewAttendance {
  id: number;
  areaId: number;
  userId: number;
  event: AttendanceEvent;
  occurredAt: string;
  edited: boolean;
  source?: AttendanceSource | null;
  reason?: string | null;
  correctedAt?: string | null;
}

export interface OverviewAttendanceGap {
  id: number;
  areaId: number;
  userId: number;
  event: AttendanceEvent;
  reason?: string | null;
  recordedAt?: string | null;
}

export interface OverviewAttendanceLeave {
  id: number;
  userId: number;
  startsAt: string;
  endsAt: string;
}

export interface OverviewCover {
  id: number;
  areaId: number;
  userId: number;
  assignedAt: string;
  cancelledAt?: string | null;
}

export interface OverviewFacts {
  submissions: OverviewSubmission[];
  inspections: OverviewInspection[];
  attendance: OverviewAttendance[];
  attendanceGaps?: OverviewAttendanceGap[];
  attendanceLeaves?: OverviewAttendanceLeave[];
  covers: OverviewCover[];
  people: { id: number; displayName: string }[];
  entryEvidence: OverviewEntryEvidence[];
  assignedAreas: number[];
  originsComplete: boolean;
  issueStateKnown: boolean;
}

export interface RoundObligation {
  pointId: number;
  areaId: number;
  buildingId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  buildingName: string;
  originId: number;
  start: string;
  end: string;
  activeFromUtc: string;
  finalizedAtUtc?: string | null;
  activationRevisionId: number;
  finalizedReason?: string | null;
}

export interface OverviewUnresolvedSubmission {
  id: number;
  pointId: number;
  submittedAt: string;
  placement: Placement;
  roundStart?: string | null;
  roundEnd?: string | null;
}

export interface OverviewRoundResult {
  roundKey: string;
  round: RoundObligation;
  outcome: 'notDone' | 'notInspected' | 'notReworked' | 'passed' | 'unresolved';
  submissionId?: number | null;
  inspectionId?: number | null;
  isPartial?: boolean;
  unresolvedSubmissions?: OverviewUnresolvedSubmission[] | null;
}

export interface OverviewAreaDetail {
  context: OverviewContext;
  availability: SectionAvailability;
  area: OverviewAreaRow;
  matchesGroup: boolean;
  entryEvidence: OverviewEntryEvidence[];
  currentResponsibility?: OverviewResponsibility | null;
  facts: OverviewFacts;
  points: OverviewPointDetail[];
  rounds: OverviewRoundResult[];
  selectedRoundKey?: string | null;
}
