import type { ScheduleDay, SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';

export type DatedAction = 'area' | 'breakGroup' | 'leave' | 'cancelLeave';

export interface DatedScheduleDraft {
  action: DatedAction;
  value: string;
  startsAt: string;
  endsAt: string;
  reason: string;
}

export interface DatedScheduleEditorProps {
  person: SchedulePerson;
  day: ScheduleDay;
  latest: ScheduleDay | undefined;
  phone: boolean;
  onClose: () => void;
  onCompare: () => void;
  reloadEditorWeek: () => Promise<ScheduleWeek>;
}
