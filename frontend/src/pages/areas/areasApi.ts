import type { AdminPoint } from '../../shared/api/adminSetupApi';
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
  }),
});

export const { useCreatePointMutation, useUpdatePointMutation, useSetPointActiveMutation } = areasApi;
