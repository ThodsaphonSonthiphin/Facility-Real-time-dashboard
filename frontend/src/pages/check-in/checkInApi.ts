import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import { myWorkApi } from '../../shared/api/myWorkApi';
import type { AttendanceEvent, AttendanceState } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';

/** RecordAttendanceRequest. The QR Token comes only from the URL the sign opened (facility-0059). */
export interface RecordAttendanceRequest extends GpsFix {
  qrToken: string;
  eventType: AttendanceEvent;
}

const checkInApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    recordAttendance: build.mutation<AttendanceState, RecordAttendanceRequest>({
      query: (body) => ({ url: '/attendance', method: 'POST', body, timeout: SEND_TIMEOUT_MS }),
      // The answer is the shift's attendance now: My Work's cached copy takes it, so every page agrees at once
      async onQueryStarted(_request, { dispatch, queryFulfilled }) {
        try {
          const { data } = await queryFulfilled;
          dispatch(
            myWorkApi.util.updateQueryData('getMyWork', undefined, (draft) => {
              draft.attendance = data;
            }),
          );
        } catch {
          // the page shows the failure
        }
      },
    }),
    // facility-0082: 204 when the sign works, 404 when it does not. It records nothing and never says whose sign it is
    checkSign: build.query<null, string>({
      query: (token) => `/attendance/signs/${encodeURIComponent(token)}`,
    }),
  }),
});

export const { useRecordAttendanceMutation, useCheckSignQuery } = checkInApi;
