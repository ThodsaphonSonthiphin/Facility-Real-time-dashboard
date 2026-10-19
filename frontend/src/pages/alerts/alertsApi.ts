import type { AlertKind } from '../../shared/api/alertListApi';
import { baseApi } from '../../shared/api/baseApi';

/** ReviewAlertRequest: "คุยแล้ว ปิดเตือน". note is optional and kept as the history row's reason. */
export interface ReviewRequest {
  kind: AlertKind;
  recordId: number;
  note?: string;
}

/** The alert page's actions. Each one asks for the list again, also after a refusal (another Admin may have closed it). */
const alertsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    reviewAlert: build.mutation<void, ReviewRequest>({
      query: (body) => ({ url: '/admin/alerts/review', method: 'POST', body }),
      invalidatesTags: ['Alerts'],
    }),
  }),
});

export const { useReviewAlertMutation } = alertsApi;
