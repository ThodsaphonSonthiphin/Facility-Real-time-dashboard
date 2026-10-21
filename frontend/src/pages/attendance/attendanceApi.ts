import type { BoardEvent } from '../../shared/api/attendanceBoardApi';
import { baseApi } from '../../shared/api/baseApi';
import type { AttendanceEvent, Shift } from '../../shared/api/types';

/** CorrectAttendanceRequest (facility-0051): time is Thai wall-clock "HH:mm" inside the shift's attendance window. */
export interface CorrectionRequest {
  userId: number;
  shiftDate: string;
  shift: Shift;
  eventType: AttendanceEvent;
  time: string;
  reason: string;
}

const attendanceApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** Adds a missing time or moves one. The board changes, and a missing Shift-Out alert closes, so both are asked again. */
    correctAttendance: build.mutation<BoardEvent, CorrectionRequest>({
      query: (body) => ({ url: '/admin/attendance', method: 'PUT', body }),
      invalidatesTags: ['AttendanceBoard', 'Alerts'],
    }),
  }),
});

export const { useCorrectAttendanceMutation } = attendanceApi;
