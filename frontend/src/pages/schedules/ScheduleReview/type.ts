import type { RowProblem } from '../../../shared/api/scheduleTypes';
import type { CommandUi } from '../commandRules';

export interface ScheduleChangeLine {
  id: string;
  label: string;
  before: string;
  after: string;
}

export interface ScheduleReviewProps {
  ui: CommandUi;
  beforeAfter: ScheduleChangeLine[];
  problems: RowProblem[];
  canReview: boolean;
  dirty: boolean;
  phone: boolean;
  busy: boolean;
  canRetry: boolean;
  changing: boolean;
  review: () => void;
  backToEdit: () => void;
  cancel: () => void;
  save: () => void;
  lookup: () => void;
  retry: () => void;
}
