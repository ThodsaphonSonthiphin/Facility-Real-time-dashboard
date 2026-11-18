import type { TransferResult } from '../../../shared/api/scheduleTypes';
import type { AttendanceEvent, AttendanceState, AttendanceTransfer, BlockedScan, MyWork } from '../../../shared/api/types';
import type { LocationProblem } from '../../../shared/geo/readLocation';

export type CheckInRetry =
  | { kind: 'action'; eventType: AttendanceEvent; gaps: AttendanceEvent[]; revision?: string | null }
  | { kind: 'transfer'; transferId: number; revision: string };

export type CheckInOutcome =
  | { kind: 'answered'; tone: 'success' | 'info' | 'warn'; message: string; accuracyM?: number }
  | { kind: 'failed'; message: string }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'location'; problem: LocationProblem; retry: CheckInRetry }
  | { kind: 'transfer-confirmed'; result: TransferResult };

export interface CheckInPageView {
  attendance: AttendanceState | undefined;
  ownArea: MyWork['areas'][number] | undefined;
  ownShift: MyWork['ownShift'];
  signGone: boolean;
  loadError: string | undefined;
  loading: boolean;
  choices: { eventType: AttendanceEvent; gaps: AttendanceEvent[] }[];
  gapConfirmation: Extract<CheckInRetry, { kind: 'action' }> | null;
  gapLabel: string | undefined;
  completionMessage: string | undefined;
  busy: AttendanceEvent | 'transfer' | null;
  outcome: CheckInOutcome | null;
  transfer: AttendanceTransfer | undefined;
  confirmable: boolean;
  transferPending: boolean;
  press: (eventType: AttendanceEvent) => void;
  confirmGaps: () => void;
  cancelGaps: () => void;
  confirmDestination: () => void;
  retryLocation: () => void;
}
