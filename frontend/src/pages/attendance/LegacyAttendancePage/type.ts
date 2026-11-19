import type { AttendanceEvent, Shift } from '../../../shared/api/types';

/** The design's D2 pages show 25 Areas in server order. */
export const PAGE_SIZE = 25;

/** The row, slot, event and Cleaner whose correction form is open. */
export type Editing = { areaId: number; event?: AttendanceEvent; shiftDate: string; shift: Shift; userId: number };
