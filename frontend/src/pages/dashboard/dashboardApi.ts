import { baseApi } from '../../shared/api/baseApi';
import { followScanHub } from '../../shared/api/scanHub';
import type { PointCard } from '../../shared/api/types';
import { getValidAccessToken } from '../../shared/auth/session';
import type { RootState } from '../../store';
import { replacePoint } from './boardRules';

/** The Dashboard's hub connection in hubSlice (plan 11 decision 4); its LiveBadge reads the same key. */
export const DASHBOARD_HUB = 'service-points';

const dashboardApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** facility-0058: every active point of every active Area, as the clock's shift sees it (Admin only, no QR Token). */
    getServicePoints: build.query<PointCard[], void>({
      query: () => '/service-points',
      providesTags: ['ServicePoints'],
      // #26: a pushed card replaces its point; facility-0064: every hub start and reconnect asks again
      async onCacheEntryAdded(_arg, { cacheDataLoaded, cacheEntryRemoved, updateCachedData, getCacheEntry, dispatch, getState }) {
        await followScanHub({
          key: DASHBOARD_HUB,
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onPoint: (card) => {
            let found = false;
            updateCachedData((draft) => {
              found = replacePoint(draft, card);
            });
            // A point the list lacks (just activated) needs its place in the server's order. A GET still in flight read
            // the database before this push and would overwrite the patch when it lands (invalidation waits for it)
            if (!found || getCacheEntry().status === 'pending') {
              dispatch(baseApi.util.invalidateTags(['ServicePoints']));
            }
          },
          onMissed: () => dispatch(baseApi.util.invalidateTags(['ServicePoints'])),
        });
      },
    }),
  }),
});

export const { useGetServicePointsQuery } = dashboardApi;
