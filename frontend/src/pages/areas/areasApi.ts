import type { AdminPoint, AdminSign } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';
import type { WindowDraft } from './pointRules';

/** SavePointRequest: the full list of windows, [] for none (the server refuses a missing list). */
export interface SavePointBody {
  name: string;
  roundWindows: WindowDraft[];
}

/** The D4 page's saves (facility-0045). Each one invalidates AdminSetup, so the open Area and the list refresh. */
const areasApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    createPoint: build.mutation<AdminPoint, { areaId: number; body: SavePointBody }>({
      query: ({ areaId, body }) => ({ url: `/admin/areas/${areaId}/points`, method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    updatePoint: build.mutation<AdminPoint, { pointId: number; body: SavePointBody }>({
      query: ({ pointId, body }) => ({ url: `/admin/points/${pointId}`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    setPointActive: build.mutation<AdminPoint, { pointId: number; active: boolean }>({
      query: ({ pointId, active }) => ({
        url: `/admin/points/${pointId}/${active ? 'activate' : 'deactivate'}`,
        method: 'POST',
      }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0038: applies to later scans; verdicts already stored stay. */
    setSignRadius: build.mutation<AdminSign, { signId: number; radiusM: number }>({
      query: ({ signId, radiusM }) => ({ url: `/admin/signs/${signId}/radius`, method: 'PUT', body: { radiusM } }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0006: the printed sign stops working at once; scan history is untouched. */
    regenerateQr: build.mutation<AdminSign, number>({
      query: (signId) => ({ url: `/admin/signs/${signId}/regenerate-token`, method: 'POST' }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const {
  useCreatePointMutation,
  useUpdatePointMutation,
  useSetPointActiveMutation,
  useSetSignRadiusMutation,
  useRegenerateQrMutation,
} = areasApi;
