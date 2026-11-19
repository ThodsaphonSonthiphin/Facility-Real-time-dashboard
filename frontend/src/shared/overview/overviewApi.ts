import { baseApi } from '../api/baseApi';
import type {
  OverviewAreaDetail,
  OverviewAreaList,
  OverviewArgs,
  OverviewDetailArgs,
  OverviewListArgs,
  OverviewSummary,
} from './types';

function cleanParams<T extends object>(args: T): Record<string, string | number> {
  const params: Record<string, string | number> = {};
  for (const [key, value] of Object.entries(args)) {
    if (value !== undefined) {
      params[key] = value as string | number;
    }
  }
  return params;
}

export const overviewApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getOverview: build.query<OverviewSummary, OverviewArgs>({
      query: (args) => ({ url: '/admin/overview', params: cleanParams(args) }),
      providesTags: ['Overview'],
    }),
    getOverviewAreas: build.query<OverviewAreaList, OverviewListArgs>({
      query: (args) => ({ url: '/admin/overview/areas', params: cleanParams(args) }),
      providesTags: ['Overview'],
    }),
    getOverviewArea: build.query<OverviewAreaDetail, OverviewDetailArgs>({
      query: ({ areaId, ...params }) => ({
        url: `/admin/overview/areas/${areaId}`,
        params: cleanParams(params),
      }),
      providesTags: ['Overview'],
    }),
  }),
});

export const { useGetOverviewQuery, useGetOverviewAreasQuery, useGetOverviewAreaQuery } = overviewApi;
