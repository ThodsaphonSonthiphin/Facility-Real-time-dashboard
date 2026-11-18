/**
 * The scheduling contract as the server sends it (spec §10, §11; Tasks 1–12 backend). Field names, words and number
 * formats are the server's, not this file's invention:
 *
 * - every revision is the stored integer as a decimal STRING (`ScheduleDtos.cs`, `ScheduleStore.Revision`);
 * - every date is an ISO date `yyyy-MM-dd` and every instant is a UTC ISO string ending in `Z`;
 * - the server leaves out every null field (`JsonIgnoreCondition.WhenWritingNull`), so a field typed `| null` or `?`
 *   arrives MISSING — test it with truthiness or `== null`, never `=== null` (shared/api/types.ts says the same).
 *
 * The words below are the server's own constants, copied so a screen can branch on them instead of on free-form text:
 * `ScheduleResolver.Availability*`/`Source*`, `ScheduleCommandOutcome`, `ScheduleCommandErrorCode`,
 * `ScheduleCommandKinds`, `ScheduleStore.UnknownCleanerKind`, `TransferStates`.
 */
import type { Shift } from './types';

/**
 * `ScheduleResolver.Availability*` (Application/Scheduling/ScheduleResolver.cs): the state of one resolved day.
 * `unavailable` is A27's "outside retained history coverage" — it is NOT "no plan"; `unset` is "nobody has chosen yet",
 * which is also not a working day. Spec §5/§10 forbid reading either as an empty day.
 */
export type ScheduleAvailability = 'available' | 'day-off' | 'leave' | 'unset' | 'unavailable';

/**
 * The week-level word. The five day states above, plus `empty` — `ScheduleStore.AvailabilityEmpty`, "nothing the actor
 * may read in this week" (no roster row at all). `empty` is never a day's word.
 */
export type ScheduleWeekAvailability = ScheduleAvailability | 'empty';

/** `ScheduleResolver.Source*`: where a resolved value came from — a dated choice, the applicable baseline, or nothing. */
export type ScheduleSourceKind = 'dated' | 'baseline' | 'unset' | 'unavailable';

/** `CleanerKind` (Domain/Entities/Scheduling/CleanerProfilePeriod.cs), plus `unknown` (`ScheduleStore.UnknownCleanerKind`). */
export type CleanerKind = 'Regular' | 'Relief' | 'unknown';

/** `ScheduleResolver.Problem*`: one day's conflicts. These are read-side problems, not a command's row problems. */
export type ScheduleDayProblemCode =
  | 'dated-group-without-area'
  | 'dated-area-on-day-off'
  | 'dated-group-on-non-working'
  | 'transfer-origin-mismatch';

/** `ScheduleCommandOutcome`: only `committed` means the stored facts changed (spec §11). */
export type ScheduleCommandOutcome =
  | 'committed'
  | 'rejected'
  | 'command-payload-mismatch'
  | 'command-expired'
  | 'command-not-authorised'
  | 'command-unknown-kind';

/** `ScheduleCommandErrorCode`: what one refused row is wrong about. */
export type RowProblemCode = 'stale-revision' | 'row-invalid';

/** `ScheduleCommandKinds` (Api/Scheduling/ScheduleCommandHandlers.cs): every kind the engine's handlers accept. */
export type ScheduleCommandKind =
  | 'set-weekly-days-off'
  | 'set-regular-baseline'
  | 'set-relief-baseline'
  | 'set-dated-area'
  | 'set-dated-break'
  | 'record-leave'
  | 'cancel-leave'
  | 'correct-plan'
  | 'assign-relief'
  | 'instruct-transfer'
  | 'cancel-transfer';

/** `TransferStates` (Api/Scheduling/EffectiveDuty.cs): one transfer instruction's state. */
export type TransferState = 'instructed' | 'confirmed' | 'cancelled';

/** One resolved value: what it is (an Area id or a break group), which row supplied it, and that row's revision. */
export interface ScheduleValue {
  /** The Area id (area) or the break group number (breakGroup); missing when nothing supplied it. */
  value?: number | null;
  /** The dated row's id or the baseline cell's id; missing when nothing supplied it. */
  sourceId?: number | null;
  sourceKind: ScheduleSourceKind;
  /** The stored revision as a decimal string; "0" when no row supplied the value. */
  revision: string;
}

/** One conflict or gap a screen has to show rather than resolve by itself (spec §5). */
export interface ScheduleDayProblem {
  code: ScheduleDayProblemCode;
  message: string;
  date: string;
}

/**
 * One resolved day with its provenance per field. `shift` is the shift that day's slot belongs to, resolved by the
 * server exactly as a command resolves it, so a client names the row it read instead of guessing (missing when the
 * person has no dated identity and no compatibility projection for that date).
 */
export interface ScheduleDay {
  date: string;
  shift?: Shift | null;
  area: ScheduleValue;
  breakGroup: ScheduleValue;
  isDayOff: boolean;
  isLeave: boolean;
  isUnset: boolean;
  availability: ScheduleAvailability;
  problems: ScheduleDayProblem[];
}

/**
 * One person's week. `days` holds only the dates this reader may see: seven for an Admin or the person themselves, and
 * only the covered dates for a Supervisor whose dated period starts or ends mid-week (spec §3). Key by `date`, never by
 * index, and never read fewer days as "no plan".
 */
export interface SchedulePerson {
  userId: number;
  displayName: string;
  cleanerKind: CleanerKind;
  homeBuildingId?: number | null;
  /** The week-header fact: false is "nobody has been asked", not "nobody works" (ADR facility-0190). */
  hasWeeklyChoice: boolean;
  weeklyRevision: string;
  regularBaselineRevision: string;
  reliefBaselineRevision: string;
  days: ScheduleDay[];
}

/**
 * One relief duty of the read week (Task 7). A cancelled row is retained with `isCancelled`, and a row never carries
 * the source team's schedule: only the codes and names needed to describe this duty.
 */
export interface ScheduleRelief {
  id: number;
  userId: number;
  displayName: string;
  shiftDate: string;
  shift: Shift;
  areaId: number;
  areaCode: string;
  destinationBuildingId: number;
  destinationBuildingCode: string;
  breakGroup?: number | null;
  replacedUserId?: number | null;
  replacedDisplayName?: string | null;
  sourceBuildingId: number;
  sourceBuildingCode: string;
  sourceSupervisorId?: number | null;
  sourceSupervisorName?: string | null;
  /** The destination actor's attestation that the source agreed outside the web; never a source signature. */
  agreementReported: boolean;
  isCancelled: boolean;
  revision: string;
}

/**
 * `ScheduleWeekDto`: one authorised week as a screen reads it. `availability` is the week's word, not a day's.
 * `relief` is a top-level list because a relief row belongs to a destination Area and a person who is not on that
 * building's roster, so it cannot live inside a person's week.
 */
export interface ScheduleWeek {
  weekStart: string;
  /** An opaque digest of every row the week was built from; it moves when the week changes. */
  revision: string;
  /** The last successful read's server time (UTC ISO). */
  readAt: string;
  availability: ScheduleWeekAvailability;
  rows: SchedulePerson[];
  relief: ScheduleRelief[];
}

/** `ReliefCandidateDto` (spec §6, A09): the minimum a recorder needs to see before recording a loan. */
export interface ReliefCandidate {
  userId: number;
  employeeId: string;
  displayName: string;
  homeBuildingId: number;
  homeBuildingCode: string;
  shift: Shift;
  eligible: boolean;
  status: string;
  conflicts: string[];
}

/** `ScheduleCommandTokenDto`: the short-lived identity a mutation is submitted under (spec §11). */
export interface ScheduleCommandToken {
  commandId: string;
  token: string;
  expiresAt: string;
}

/**
 * `ScheduleCommandEnvelope`: one submitted command. `commandId` must be the token's own id — the client never chooses
 * it — and `expectedRevisions` is the revision map the client read.
 */
export interface ScheduleCommandEnvelope {
  token: string;
  commandId: string;
  expectedRevisions: Record<string, string>;
  kind: ScheduleCommandKind;
  payload: unknown;
}

/** One row a command refused. */
export interface RowProblem {
  rowId: string;
  code: RowProblemCode;
  message: string;
}

/**
 * `CommandResult`: what a command did, or why it refused. A refusal names no other team's rows and never reports a
 * partial success. It arrives with a non-2xx status too (409 rejected/payload-mismatch, 410 expired, 403 not
 * authorised, 400 otherwise), so a caller reads it out of the error's `data` as well as out of a success.
 */
export interface CommandResult {
  commandId: string;
  outcome: ScheduleCommandOutcome;
  revisions: Record<string, string>;
  problems: RowProblem[];
}

/** `POST /api/area-transfers/{id}/confirm` (spec §9): the page's read revision, the scanned sign, and the location. */
export interface ConfirmTransferRequest {
  expectedRevision: string;
  qrToken: string;
  latitude: number;
  longitude: number;
  accuracyM: number;
}

/** `TransferResult`: the instruction's identity, its state word, the actual arrival time, and the revision it is valid against. */
export interface TransferResult {
  id: number;
  state: TransferState;
  confirmedAt?: string | null;
  revision: string;
}

/** `ScheduleInvalidation`: ScanHub's "ScheduleChanged" payload — the identity of the command and nothing else (spec §12). */
export interface ScheduleInvalidation {
  commandId: string;
}
