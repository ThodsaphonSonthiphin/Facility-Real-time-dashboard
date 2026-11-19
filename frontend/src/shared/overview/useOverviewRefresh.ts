import { skipToken } from '@reduxjs/toolkit/query';
import { useAppSelector } from '../../store/hooks';
import { selectHubRecovery } from '../../store/slices/hubSlice';
import { overviewHubKey } from './overviewApi';
import type { OverviewContext, OverviewDetailArgs, OverviewListArgs } from './types';

export function useOverviewRefresh(context?: OverviewContext): { pollingInterval: number; refetchOnFocus: boolean } {
  const shouldRefresh = context?.phase !== 'closed';
  return { pollingInterval: shouldRefresh ? 30_000 : 0, refetchOnFocus: shouldRefresh };
}

export function useOverviewRecovering(args: OverviewListArgs | OverviewDetailArgs | typeof skipToken): boolean {
  const key = args === skipToken ? undefined : overviewHubKey(args);
  return useAppSelector((state) => key !== undefined && selectHubRecovery(state, key) !== undefined);
}
