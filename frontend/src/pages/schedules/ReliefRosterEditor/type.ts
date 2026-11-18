import type { ReliefAssignmentEditorProps } from '../ReliefAssignmentEditor/type';
import type { weekdays } from '../commandRules';
import type { ManagedWeekArgs } from '../schedulesApi';

export interface ReliefRosterEditorProps extends ReliefAssignmentEditorProps {
  selection: ManagedWeekArgs;
}

export type ReliefWeekday = typeof weekdays[number];
export type ReliefCell = { weekday: ReliefWeekday; areaId?: number; isDayOff?: boolean };
