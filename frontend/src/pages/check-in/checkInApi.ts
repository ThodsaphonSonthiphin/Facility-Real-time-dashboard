import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import { myWorkApi } from '../../shared/api/myWorkApi';
import type { ConfirmTransferRequest, TransferResult } from '../../shared/api/scheduleTypes';
import type { AttendanceEvent, AttendanceState } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';

/**
 * RecordAttendanceRequest. The QR Token comes only from the URL the sign opened (facility-0059). Spec §8: a Numbered6
 * slot also carries its contract marker and the state revision the page read, plus the explicit skip markers of any
 * intermediate action it steps over — never a guessed time.
 */
export interface RecordAttendanceRequest extends GpsFix {
  qrToken: string;
  eventType: AttendanceEvent;
  lifecycleVersion?: string;
  stateRevision?: string;
  confirmGaps?: AttendanceEvent[];
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
    checkSign: build.query<{ canConfirmTransfer: boolean } | null, { token: string; transferId?: number }>({
      query: ({ token, transferId }) => ({
        url: `/attendance/signs/${encodeURIComponent(token)}`,
        params: transferId === undefined ? undefined : { transferId },
      }),
    }),
    confirmTransferArrival: build.mutation<TransferResult, ConfirmTransferRequest & { transferId: number }>({
      query: ({ transferId, ...body }) => ({
        url: `/area-transfers/${transferId}/confirm`,
        method: 'POST',
        body,
        timeout: SEND_TIMEOUT_MS,
      }),
    }),
  }),
});

export const { useRecordAttendanceMutation, useCheckSignQuery, useConfirmTransferArrivalMutation } = checkInApi;
