import type { ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { TeamMissingTimeProps } from '../TeamMissingTime/type';

export interface TransferInstructionProps extends TeamMissingTimeProps {
  latest: ScheduleWeek | undefined;
  reloadEditorWeek: () => Promise<ScheduleWeek>;
}
