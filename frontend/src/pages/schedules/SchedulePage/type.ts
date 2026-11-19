import type { ScheduleDay, SchedulePerson, ScheduleTransferSlot, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { Notice } from '../../../shared/components/Toast';
import type { CoverSlot } from '../../../shared/cover/coverSlots';
import type { TeamSlotArgs } from '../schedulesApi';
import type { ScheduleRole, ScheduleSelection } from '../scheduleRules';

/**
 * What the page's hook hands its JSX: the answer, what to say while there is none, and the one week control. Nothing here
 * fetches or decides authority — the server's own scoped read is the only authority, and the hook passes it through.
 */
export interface SchedulePageView {
  /** The role whose week this is, so the body can show the words for it without a second parameter. */
  role: ScheduleRole;
  week: ScheduleWeek | undefined;
  latest: ScheduleWeek | undefined;
  standaloneTransfers: ScheduleTransferSlot[];
  /** The selection actually asked for, after a restored filter the server has nothing readable for was dropped. */
  selection: ScheduleSelection;
  /** True when a restored building was dropped this read, so the page can say the filter changed rather than pretend. */
  filterDropped: boolean;
  isLoading: boolean;
  /** Another answer is on its way over an older one already on screen. */
  isFetching: boolean;
  error: unknown;
  /**
   * Spec §12: the read on screen may be older than the stored plan. It stays until a read that started after the push
   * succeeded; a reconnect, a request start or a failed read never clears it.
   */
  mayBeStale: boolean;
  /** What the hub said about this read's connection, for the footnote beside it. */
  hubStatus: 'connecting' | 'live' | 'reconnecting';
  /** Ask again now: the "อัปเดต" control and a failed read's retry. */
  refetch: () => void;
  reloadEditorWeek: () => Promise<ScheduleWeek>;
  /** The week the person chose: it is what returning to the page restores (facility-0181, 0185, 0218). */
  chooseWeek: (weekStart: string) => void;
  chooseBuilding: (buildingId: number | undefined) => void;
  chooseShift: (shift: ScheduleSelection['shift']) => void;
  /** The previous or next week, always a Monday (a week is read one at a time). */
  goWeek: (weeks: number) => void;
  phone: boolean;
  editor: OpenScheduleEditor | null;
  openRelief: (kind: 'relief-roster' | 'relief-assignment' | 'relief-loan') => void;
  openWeekly: () => void;
  openBaseline: (person: SchedulePerson) => void;
  openDated: (person: SchedulePerson, day: ScheduleDay) => void;
  openTeamSlot: (kind: 'missing-time' | 'transfer', slot: TeamSlotArgs, displayName: string) => void;
  closeEditor: () => void;
  dateAction: (day: ScheduleDay) => string;
  businessDate: string;
  selectedCell: SelectedScheduleCell | null;
  selectCell: (userId: number, date: string) => void;
  /** Admin emergency cover drawer (facility-0225). */
  coverSlot: CoverSlot | undefined;
  coverOpen: boolean;
  coverNotice: Notice | null;
  openCover: () => void;
  closeCover: () => void;
  onCoverDone: (text: string) => void;
  clearCoverNotice: () => void;
}

export interface SelectedScheduleCell {
  userId: number;
  date: string;
}

/** One table row of the management view: a person and the days the reader may see (seven, or fewer mid-week). */
export interface ScheduleRow {
  person: SchedulePerson;
  days: ScheduleDay[];
}

export type OpenScheduleEditor =
  | { kind: 'relief-roster'; week: ScheduleWeek }
  | { kind: 'relief-assignment'; week: ScheduleWeek }
  | { kind: 'relief-loan'; week: ScheduleWeek }
  | { kind: 'weekly'; week: ScheduleWeek }
  | { kind: 'baseline'; week: ScheduleWeek; person: SchedulePerson }
  | { kind: 'dated'; person: SchedulePerson; day: ScheduleDay }
  | { kind: 'missing-time'; slot: TeamSlotArgs; displayName: string }
  | { kind: 'transfer'; slot: TeamSlotArgs; displayName: string };
