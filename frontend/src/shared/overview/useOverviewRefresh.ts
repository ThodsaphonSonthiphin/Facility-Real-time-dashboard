import type { OverviewContext } from './types';

export function useOverviewRefresh(context?: OverviewContext): { pollingInterval: number; refetchOnFocus: boolean } {
  const shouldRefresh = context?.phase !== 'closed';
  return { pollingInterval: shouldRefresh ? 30_000 : 0, refetchOnFocus: shouldRefresh };
}
