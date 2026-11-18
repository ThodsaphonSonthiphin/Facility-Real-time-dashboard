import type { ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { Shift } from '../../../shared/api/types';

export interface ReliefAssignmentRequest {
  userId: number;
  areaId: number;
  shiftDate: string;
  shift: Shift;
  replacedUserId?: number;
  breakGroup?: number;
  sourceBuildingId: number;
  sourceSupervisorId?: number;
  agreementReported: boolean;
  reason?: string;
}

export interface ReliefAssignmentEditorProps {
  phone: boolean;
  week: ScheduleWeek;
  latest?: ScheduleWeek;
  onClose: () => void;
  onCompare: () => void;
  reloadEditorWeek: () => Promise<ScheduleWeek>;
}
