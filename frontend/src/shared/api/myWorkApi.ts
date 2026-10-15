import { baseApi } from './baseApi';
import type { MyWork } from './types';

/**
 * facility-0052: GET /api/my-work feeds My Work, the check-in page and the scan page. Cleaners get no live updates,
 * so every page that shows it passes `freshOnOpen` and asks the server again each time it opens.
 */
export const myWorkApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getMyWork: build.query<MyWork, void>({
      query: () => '/my-work',
    }),
  }),
});

export const { useGetMyWorkQuery } = myWorkApi;

/** Hook options for every Cleaner page: never act on an answer from an earlier visit. */
export const freshOnOpen = { refetchOnMountOrArgChange: true } as const;
