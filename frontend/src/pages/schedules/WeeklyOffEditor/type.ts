import type { ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { ManagedWeekArgs } from '../schedulesApi';

export interface WeeklyOffDraft {
  weekStart: string;
  choices: Record<number, string[]>;
}

export interface WeeklyOffEditorProps {
  week: ScheduleWeek;
  latest: ScheduleWeek | undefined;
  selection: ManagedWeekArgs;
  phone: boolean;
  onClose: () => void;
  onCompare: () => void;
  reloadEditorWeek: () => Promise<ScheduleWeek>;
}
