import type { AreaDetail, ShiftPattern } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';

/** CreateAreaRequest / UpdateAreaRequest. A null Cleaner leaves that shift empty and takes its Cleaner off. */
export interface AreaFormBody {
  name: string;
  buildingId: number;
  shiftPattern: ShiftPattern;
  dayCleanerId: number | null;
  nightCleanerId: number | null;
}

/** The D10 page's saves (facility-0045). The code goes only with a new Area: it is fixed once created. */
const areaFormApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    createArea: build.mutation<AreaDetail, AreaFormBody & { code: string }>({
      query: (body) => ({ url: '/admin/areas', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    updateArea: build.mutation<AreaDetail, { areaId: number; body: AreaFormBody }>({
      query: ({ areaId, body }) => ({ url: `/admin/areas/${areaId}`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    setAreaActive: build.mutation<AreaDetail, { areaId: number; active: boolean }>({
      query: ({ areaId, active }) => ({
        url: `/admin/areas/${areaId}/${active ? 'activate' : 'deactivate'}`,
        method: 'POST',
      }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useCreateAreaMutation, useUpdateAreaMutation, useSetAreaActiveMutation } = areaFormApi;
