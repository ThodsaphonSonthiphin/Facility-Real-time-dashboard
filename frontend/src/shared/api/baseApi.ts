import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import type { RootState } from '../../store';
import { refreshSession } from '../auth/session';

/** facility-0052: a scan or attendance send with no answer in 20 s counts as not sent; the page offers to send again. */
export const SEND_TIMEOUT_MS = 20_000;

const rawBaseQuery = fetchBaseQuery({
  // Absolute, on this page's own origin (facility-0067): RTK Query builds a Request, and a relative URL fails outside a browser (tests)
  baseUrl: `${window.location.origin}/api`,
  credentials: 'same-origin',
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as RootState).auth.accessToken;
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    return headers;
  },
});

/** Login, refresh and logout answer 401 for a wrong secret or a dead cookie; refreshing then would be wrong. */
function isAuthCall(args: string | FetchArgs): boolean {
  return (typeof args === 'string' ? args : args.url).startsWith('/auth/');
}

/** facility-0023: on 401, refresh once (shared, facility-0015), then retry the request once. */
export const baseQueryWithReauth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
  args,
  api,
  extraOptions,
) => {
  let result = await rawBaseQuery(args, api, extraOptions);
  if (result.error?.status === 401 && !isAuthCall(args) && (await refreshSession(api.dispatch)) === 'refreshed') {
    result = await rawBaseQuery(args, api, extraOptions);
  }
  return result;
};

/** facility-0022: one API, one cache. Each page adds its endpoints with injectEndpoints in pages/<page>/<page>Api.ts. */
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithReauth,
  // MyBuilding: refetched where a live push may have been missed (facility-0064 item 2).
  // AdminSetup: every Admin setup read; every Admin save invalidates it (plan 10 decision 3)
  tagTypes: ['MyBuilding', 'AdminSetup'],
  endpoints: () => ({}),
});
