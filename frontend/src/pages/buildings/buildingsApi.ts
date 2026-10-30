import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';
import type { BuildingSupervisor } from './type';

/** The buildings page's saves (facility-0090). The code goes only with a new building: it is fixed once created. */
const buildingsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** Existing Admin accounts API; only public Supervisor assignment fields are consumed. */
    getBuildingSupervisors: build.query<BuildingSupervisor[], void>({
      query: () => '/admin/accounts',
      providesTags: ['AdminSetup'],
    }),
    createBuilding: build.mutation<ManagedBuilding, { code: string; name: string }>({
      query: (body) => ({ url: '/admin/buildings', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    renameBuilding: build.mutation<ManagedBuilding, { buildingId: number; name: string }>({
      query: ({ buildingId, name }) => ({ url: `/admin/buildings/${buildingId}`, method: 'PUT', body: { name } }),
      invalidatesTags: ['AdminSetup'],
    }),
    setBuildingActive: build.mutation<ManagedBuilding, { buildingId: number; active: boolean }>({
      query: ({ buildingId, active }) => ({
        url: `/admin/buildings/${buildingId}/${active ? 'activate' : 'deactivate'}`,
        method: 'POST',
      }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useGetBuildingSupervisorsQuery, useCreateBuildingMutation, useRenameBuildingMutation, useSetBuildingActiveMutation } = buildingsApi;
