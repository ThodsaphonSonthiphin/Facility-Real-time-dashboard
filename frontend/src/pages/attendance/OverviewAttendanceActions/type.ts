import type { Shift } from '../../../shared/api/types';

export interface OverviewAttendanceActionsProps {
  areaId: number;
  shiftDate: string;
  shift: Shift;
}

export type AttendanceActionKind = 'correction' | 'cover';

export interface OpenAttendanceAction {
  id: number;
  kind: AttendanceActionKind;
  userId?: number;
}
