import type { Shift } from '../../../shared/api/types';

export interface OverviewAttendanceActionsProps {
  areaId: number;
  shiftDate: string;
  shift: Shift;
}

export interface OpenAttendanceAction {
  id: number;
  userId: number;
}
