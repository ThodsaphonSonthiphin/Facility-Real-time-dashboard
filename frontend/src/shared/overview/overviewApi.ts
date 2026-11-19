import type { RootState } from '../../store';
import { baseApi } from '../api/baseApi';
import { ADMIN_HUB_SERVER_TIMEOUT_MS, followScanHub, type ScanHubFollow } from '../api/scanHub';
import { getValidAccessToken } from '../auth/session';
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

export function overviewHubKey(args: OverviewListArgs | OverviewDetailArgs): string {
  return `overview:${JSON.stringify(args, Object.keys(args).sort())}`;
}

function overviewTag(args: OverviewListArgs | OverviewDetailArgs) {
  return { type: 'Overview' as const, id: overviewHubKey(args) };
}

type OverviewCacheLifecycle = Pick<ScanHubFollow, 'cacheDataLoaded' | 'cacheEntryRemoved' | 'dispatch'>
  & { getState: () => unknown };

function followOverview(
  args: OverviewListArgs | OverviewDetailArgs,
  { cacheDataLoaded, cacheEntryRemoved, dispatch, getState }: OverviewCacheLifecycle,
): Promise<void> | void {
  // The list's default-slot read is immediately replaced by the canonical date and shift.
  if (!args.shiftDate || !args.shift) return;
  const invalidate = () => { dispatch(baseApi.util.invalidateTags(['Overview'])); };
  return followScanHub({
    key: overviewHubKey(args),
    cacheDataLoaded,
    cacheEntryRemoved,
    dispatch,
    accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
    serverTimeoutMs: ADMIN_HUB_SERVER_TIMEOUT_MS,
    // The point payload describes the clock's shift, which can differ from the slot that changed.
    onPoint: invalidate,
    // Includes actual transfer arrivals and corrections. ScheduleChanged also carries instructions.
    onAttendance: invalidate,
    onOverview: invalidate,
    // Only this connection may have missed pushes. Keep unrelated inactive selections cached.
    onMissed: () => { dispatch(baseApi.util.invalidateTags([overviewTag(args)])); },
  });
}

export const overviewApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getOverview: build.query<OverviewSummary, OverviewArgs>({
      query: (args) => ({ url: '/admin/overview', params: cleanParams(args) }),
      providesTags: ['Overview'],
    }),
    getOverviewAreas: build.query<OverviewAreaList, OverviewListArgs>({
      query: (args) => ({ url: '/admin/overview/areas', params: cleanParams(args) }),
      providesTags: (_result, _error, args) => [overviewTag(args)],
      onCacheEntryAdded: followOverview,
    }),
    getOverviewArea: build.query<OverviewAreaDetail, OverviewDetailArgs>({
      query: ({ areaId, ...params }) => ({
        url: `/admin/overview/areas/${areaId}`,
        params: cleanParams(params),
      }),
      onCacheEntryAdded: followOverview,
      providesTags: (_result, _error, args) => [overviewTag(args)],
    }),
  }),
});

export const { useGetOverviewQuery, useGetOverviewAreasQuery, useGetOverviewAreaQuery } = overviewApi;
