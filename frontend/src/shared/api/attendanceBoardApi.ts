import type { RootState } from '../../store';
import { getValidAccessToken } from '../auth/session';
import type { AssignedCleaner } from './adminSetupApi';
import { baseApi } from './baseApi';
import { followScanHub } from './scanHub';
import type { AttendanceEvent, Shift } from './types';

/**
 * The attendance board (facility-0027, 0050), read by the attendance page (D2) and the alert page's "Areas with nobody"
 * (D3). Fields the server sends as null arrive missing, so they are optional here.
 */

/** Whether someone is in the Area this shift, never late or early (facility-0050). */
export type AreaAttendanceState = 'Empty' | 'Working' | 'OnBreak' | 'Done' | 'Covered';

/** BoardEventDto. source AdminAdd or AdminEdit is the "แก้โดย Admin" badge (facility-0051); withinRadius false is a flag. */
export interface BoardEvent {
  id: number;
  eventType: AttendanceEvent;
  occurredAt: string;
  source: 'Scan' | 'AdminAdd' | 'AdminEdit';
  withinRadius?: boolean;
  distanceM?: number;
}

/** BoardCoverDto: a Cleaner covering this Area for the shift (facility-0041), with their own Area and their Shift-In. */
export interface BoardCover {
  id: number;
  userId: number;
  employeeId: string;
  displayName: string;
  ownAreaCode?: string;
  assignedAt: string;
  shiftInAt?: string;
}

/** BoardRowDto. cleaner is the Area's regular Cleaner for the shift (missing when none); events are theirs. */
export interface BoardRow {
  areaId: number;
  areaCode: string;
  areaName: string;
  buildingCode: string;
  cleaner?: AssignedCleaner;
  state: AreaAttendanceState;
  events: BoardEvent[];
  covers: BoardCover[];
}

/** BoardSummaryDto: required = present + empty + covered. */
export interface BoardSummary {
  required: number;
  present: number;
  empty: number;
  covered: number;
}

/** AttendanceBoardDto: every active Area that works the shift, the Areas with nobody first. */
export interface AttendanceBoard {
  shiftDate: string;
  shift: Shift;
  shiftEnded: boolean;
  summary: BoardSummary;
  rows: BoardRow[];
}

/** Both shiftDate and shift, or neither (then the shift on the clock); buildingId narrows to one building. */
export interface BoardArgs {
  shiftDate?: string;
  shift?: Shift;
  buildingId?: number;
}

/** The tag of one shift's boards, whatever building they show: AttendanceRecorded names the shift (plan 11 decision 5). */
export function boardTag(shiftDate: string, shift: Shift) {
  return { type: 'AttendanceBoard' as const, id: `${shiftDate}:${shift}` };
}

/** One hub connection per board on screen (plan 11 decision 4); the page's LiveBadge reads the same key. */
export function attendanceHubKey({ shiftDate, shift, buildingId }: BoardArgs): string {
  return `attendance-board:${shiftDate ?? 'now'}:${shift ?? ''}:${buildingId ?? 'all'}`;
}

const attendanceBoardApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getAttendanceBoard: build.query<AttendanceBoard, BoardArgs>({
      query: ({ shiftDate, shift, buildingId }) => ({ url: '/admin/attendance', params: { shiftDate, shift, buildingId } }),
      providesTags: (board) => (board ? [boardTag(board.shiftDate, board.shift)] : ['AttendanceBoard']),
      // facility-0027 rule 5: a check-in, a break or a correction asks again for that shift's boards; facility-0064:
      // every hub start and reconnect asks again. Invalidation waits for a GET in flight, so no older answer wins
      async onCacheEntryAdded(args, { cacheDataLoaded, cacheEntryRemoved, dispatch, getState }) {
        await followScanHub({
          key: attendanceHubKey(args),
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onAttendance: (change) => dispatch(baseApi.util.invalidateTags([boardTag(change.shiftDate, change.shift)])),
          onMissed: () => dispatch(baseApi.util.invalidateTags(['AttendanceBoard'])),
        });
      },
    }),
  }),
});

export const { useGetAttendanceBoardQuery } = attendanceBoardApi;
