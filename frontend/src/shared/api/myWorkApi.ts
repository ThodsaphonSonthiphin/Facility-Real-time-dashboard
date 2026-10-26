import { followScanHub } from './scanHub';
import { baseApi } from './baseApi';
import { getValidAccessToken } from '../auth/session';
import type { RootState } from '../../store';
import type { MyWork } from './types';

/** My Work's hub connection in hubSlice (plan 11 decision 4); the page's footer reads the same key. */
export const MY_WORK_HUB = 'my-work';

/**
 * facility-0052 as amended 2026-10-26: GET /api/my-work feeds My Work, the check-in page and the scan page. The hub
 * tells a Cleaner when a point of their Area (or of an Area they cover) changes, such as the Supervisor's inspection, and
 * the page asks again. It asks instead of patching the pushed card, because the hub sends the card as the clock's shift
 * sees it and a Cleaner in the hour before or after their shift sees another one. Every page that shows it also passes
 * `freshOnOpen` (baseApi) and asks the server again each time it opens.
 */
export const myWorkApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getMyWork: build.query<MyWork, void>({
      query: () => '/my-work',
      providesTags: ['MyWork'],
      async onCacheEntryAdded(_arg, { cacheDataLoaded, cacheEntryRemoved, dispatch, getState }) {
        const askAgain = () => dispatch(baseApi.util.invalidateTags(['MyWork']));
        await followScanHub({
          key: MY_WORK_HUB,
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onPoint: askAgain,
          onMissed: askAgain,
        });
      },
    }),
  }),
});

export const { useGetMyWorkQuery } = myWorkApi;
