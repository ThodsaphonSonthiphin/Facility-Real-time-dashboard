import type { AlertKind } from '../../shared/api/alertListApi';
import { baseApi } from '../../shared/api/baseApi';

/** ReviewAlertRequest: "คุยแล้ว ปิดเตือน". note is optional and kept as the history row's reason. */
interface ReviewRequest {
  kind: AlertKind;
  recordId: number;
  note?: string;
}

const alertsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    reviewAlert: build.mutation<void, ReviewRequest>({
      query: (body) => ({ url: '/admin/alerts/review', method: 'POST', body }),
      invalidatesTags: ['Alerts'],
    }),
  }),
});

export const { useReviewAlertMutation } = alertsApi;
