import type { ScheduleChangeLine } from '../ScheduleReview/type';

export interface ScheduleConflictProps {
  draft: ScheduleChangeLine[];
  latest?: ScheduleChangeLine[];
  size: 'desk' | 'phone';
  disabled: boolean;
  onCompare: () => void;
  onUseLatest: () => void;
}
