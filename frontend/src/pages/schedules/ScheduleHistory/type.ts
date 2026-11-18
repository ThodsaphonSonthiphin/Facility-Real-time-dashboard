import type { ScheduleCorrection } from '../../../shared/api/scheduleTypes';

export interface ScheduleHistoryProps {
  corrections: readonly ScheduleCorrection[];
}
