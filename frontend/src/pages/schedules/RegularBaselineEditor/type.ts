import type { SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { Shift } from '../../../shared/api/types';
import type { ManagedWeekArgs } from '../schedulesApi';

export interface RegularBaselineDraft {
  effectiveFrom: string;
  shift: Shift | '';
  areaId: string;
  breakGroup: string;
}

export interface RegularBaselineEditorProps {
  person: SchedulePerson;
  week: ScheduleWeek;
  latest: ScheduleWeek | undefined;
  selection: ManagedWeekArgs;
  phone: boolean;
  onClose: () => void;
  onCompare: () => void;
  reloadEditorWeek: () => Promise<ScheduleWeek>;
}
