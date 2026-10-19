import type { AreaDetail, AreaSummary } from '../../shared/api/adminSetupApi';
import { baseApi } from '../../shared/api/baseApi';

const printSignsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * The Areas to print: one Area by id, or "all" (facility-0021 item 6: every active one, not only what is on
     * screen). The server has no all-signs endpoint, so "all" asks for each active Area's detail.
     */
    getPrintAreas: build.query<AreaDetail[], string>({
      async queryFn(which, _api, _extraOptions, baseQuery) {
        let ids: number[];
        if (which === 'all') {
          const list = await baseQuery('/admin/areas');
          if (list.error) {
            return { error: list.error };
          }
          ids = (list.data as AreaSummary[]).filter((area) => area.isActive).map((area) => area.id);
        } else {
          ids = [Number(which)];
        }
        const answers = await Promise.all(ids.map((id) => baseQuery(`/admin/areas/${id}`)));
        const failed = answers.find((answer) => answer.error);
        if (failed?.error) {
          return { error: failed.error };
        }
        return { data: answers.map((answer) => answer.data as AreaDetail) };
      },
      providesTags: ['AdminSetup'],
    }),
  }),
});

export const { useGetPrintAreasQuery } = printSignsApi;
