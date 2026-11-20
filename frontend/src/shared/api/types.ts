/**
 * Shapes the server sends and accepts (facility-0022). Enums arrive as strings (JsonStringEnumConverter),
 * times as UTC ISO strings ending in "Z". Types used by one page only live in that page's folder.
 * The server leaves out every null field (JsonIgnoreCondition.WhenWritingNull, REST and hub alike): a field typed
 * `| null` or `?` arrives missing, so code tests it with truthiness or `== null`, never `=== null`.
 */

/** The "role" of a login or refresh answer (AuthClaims.ToClaimValue on the server). */
export type Role = 'cleaner' | 'supervisor' | 'admin';

/** AuthUserDto. username is the employee ID, or the Admin's username. */
export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: Role;
}

/** Answer of POST /api/auth/login and /api/auth/refresh. The refresh token is never here: it is an HttpOnly cookie (facility-0013). */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** facility-0054: Cleaners and Supervisors send employeeId + phone; the Admin sends username + password. */
export type LoginRequest = { employeeId: string; phone: string } | { username: string; password: string };

/** facility-0040: Day 07:00–19:00, Night 19:00–07:00 (Asia/Bangkok). */
export type Shift = 'Day' | 'Night';

export type AttendanceEvent =
  | 'ShiftIn'
  | 'Break1Out'
  | 'Break1In'
  | 'Break2Out'
  | 'Break2In'
  | 'ShiftOut';

/** AttendanceEntryDto. withinRadius false is a Geofence Flag; null means the sign has no coordinates yet. */
export interface AttendanceEntry {
  eventType: AttendanceEvent;
  occurredAt: string;
  source: 'Scan' | 'AdminAdd' | 'AdminEdit' | 'SupervisorAdd';
  withinRadius: boolean | null;
  createdAt?: string | null;
}

/** `AttendanceSource` as the wire word, shared by an entry and an action. */
export type AttendanceSource = AttendanceEntry['source'];

/** `AttendanceStateView` words (spec §8): what one action of the lifecycle is right now. */
export type AttendanceActionState = 'recorded' | 'skipped' | 'missing' | 'not-applicable' | 'pending';

/** AttendanceActionDto: one action of the slot's ordered lifecycle and its state (expectedAt is the plan, never a scan). */
export interface AttendanceAction {
  eventType: AttendanceEvent;
  state: AttendanceActionState;
  occurredAt?: string | null;
  source?: AttendanceSource | null;
  expectedAt?: string | null;
}

/** `AttendanceGapStatus`: a stored skip marker, or the later real time that resolved it. */
export type AttendanceGapStatus = 'Skipped' | 'Resolved';

/** AttendanceGapDto: one stored gap row with no invented time of its own. */
export interface AttendanceGap {
  eventType: AttendanceEvent;
  status: AttendanceGapStatus;
  reason?: string | null;
  recordedAt?: string | null;
}

/** AttendanceChoiceDto: one action the Cleaner may record next, and the intermediate gaps it steps over. */
export interface AttendanceChoice {
  action: AttendanceEvent;
  requiredGaps: AttendanceEvent[];
}

/** AttendanceTransferDto (spec §9): the slot's midshift instruction and the revision a confirmation echoes back. */
export interface AttendanceTransfer {
  id: number;
  fromAreaId: number;
  toAreaId: number;
  effectiveAt: string;
  state: 'instructed' | 'confirmed' | 'cancelled';
  confirmedAt?: string | null;
  revision: string;
}

/** The current six-action scan state and the revision used for writes. */
export interface AttendanceState {
  shiftDate?: string | null;
  shift?: Shift | null;
  events: AttendanceEntry[];
  outcome?: 'Recorded' | 'AlreadyRecorded' | 'TooSoon' | null;
  stateRevision?: string | null;
  actions?: AttendanceAction[];
  gaps?: AttendanceGap[];
  nextActions?: AttendanceChoice[];
  dutyAreaId?: number | null;
  dutyBreakGroup?: number | null;
  dutySource?: string | null;
  isDayOff?: boolean;
  isLeave?: boolean;
  transfer?: AttendanceTransfer | null;
  transferHistory?: AttendanceTransfer[];
  fillableActions?: AttendanceEvent[];
  canCancelTransfer?: boolean;
  /** An early final departure without recorded leave covering its actual time. */
  departureLeaveMismatch?: boolean;
}

/** facility-0047 rule 4 (PointStatusCalculator). */
export type PointStatus =
  | 'Rework'
  | 'Overdue'
  | 'PendingInspection'
  | 'Passed'
  | 'NotYetDone'
  | 'BeforeFirstRound'
  | 'OffHours';

/** facility-0046: Issue is a tag on the card, never a Point Status. */
export type CleaningStatus = 'Normal' | 'Issue';

/** facility-0047 rule 3: which round a submission counts for, decided once by the server. */
export type Placement = 'OnTime' | 'Late' | 'Rework' | 'OffRound';

/** RoundWindowDto. start and end are Thai wall-clock "HH:mm:ss", as the Admin entered them. */
export interface RoundWindow {
  id: number;
  start: string;
  end: string;
}

/** LastScanDto. */
export interface LastScan {
  submittedAt: string;
  cleanerName: string;
  placement: Placement;
  status: CleaningStatus;
}

/** IssueDto (facility-0046): shown while the latest submission reported an issue. note is missing when none was typed. */
export interface ReportedIssue {
  tags: string[];
  note?: string;
  reportedAt: string;
}

/** ReworkDto (facility-0029): what the Supervisor asked to fix, while the card is ต้องแก้ไข. */
export interface Rework {
  defect: string;
  requestedAt: string;
}

/** PointStatusDto: one point's card. It never carries the QR Token (facility-0059, 0060). Fields the server sends as null arrive missing. */
export interface PointCard {
  id: number;
  name: string;
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  status: PointStatus;
  currentRound?: RoundWindow;
  nextRound?: RoundWindow;
  lastScan?: LastScan;
  issue?: ReportedIssue;
  rework?: Rework;
}

/** MyWorkAreaDto. isCover: an Area the Cleaner covers this shift (facility-0041). */
export interface MyWorkArea {
  areaId: number;
  areaCode: string;
  areaName: string;
  isCover: boolean;
  points: PointCard[];
}

/** OwnShiftDto (facility-0077): the Cleaner's regular shift and the Thai wall-clock times its attendance opens and closes, "HH:mm:ss". */
export interface OwnShift {
  shift: Shift;
  opensAt: string;
  closesAt: string;
}

/** MyWorkDto (facility-0052): the attendance, then the own Area, then covered Areas. ownShift is missing for an account with no shift. */
export interface MyWork {
  attendance: AttendanceState;
  areas: MyWorkArea[];
  ownShift?: OwnShift;
}

/** BlockedScanResponse (403): the scanned sign belongs elsewhere and nothing was recorded (facility-0041, 0048). */
export interface BlockedScan {
  message: string;
  areaCode: string;
  areaName: string;
  reason: 'OtherArea' | 'OtherBuilding' | 'OutsideShift';
  buildingCode: string;
}

/** AttendanceChangedDto: ScanHub's "AttendanceRecorded", to Admins only (facility-0027 rule 5): that shift's board changed. */
export interface AttendanceChanged {
  areaId: number;
  shiftDate: string;
  shift: Shift;
}
