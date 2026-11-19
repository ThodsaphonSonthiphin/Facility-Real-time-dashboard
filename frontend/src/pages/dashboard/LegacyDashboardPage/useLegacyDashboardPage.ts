import { useState } from 'react';
import { useGetBuildingsQuery } from '../../../shared/api/adminSetupApi';
import { livePoll } from '../../../shared/api/baseApi';
import { useBuildingNames } from '../../../shared/api/buildingNames';
import { pointStatusLabel } from '../../../shared/utils/labels';
import { useAppDispatch, useAppSelector } from '../../../store/hooks';
import { filtersCleared } from '../../../store/slices/dashboardSlice';
import { filterPoints, groupByArea, hasProblem, isUnfiltered, statusCounts } from '../boardRules';
import { useGetServicePointsQuery } from '../dashboardApi';
import { QUIET_ROWS, type AreaSectionProps } from './type';

export function useLegacyDashboardPage() {
  const { data: points, error, isLoading: pointsLoading, refetch } = useGetServicePointsQuery(undefined, livePoll);
  // The building picker widens when the names arrive; asked here too (one shared request) so the filters, the help
  // and the cards appear together and nothing already on screen moves (layout shift, measured 2026-10-28)
  const { isLoading: buildingsLoading } = useGetBuildingsQuery();
  const isLoading = pointsLoading || buildingsLoading;
  const data = isLoading ? undefined : points;
  const filters = useAppSelector((state) => state.dashboard);
  const shown = data ? filterPoints(data, filters) : [];
  const dispatch = useAppDispatch();
  const [showAllQuiet, setShowAllQuiet] = useState(false);
  // With "เรียงพื้นที่ที่มีปัญหาขึ้นก่อน" on and nothing filtered, the Areas that need someone come first as cards and the
  // others fold into one line each; a filter or a search shows every Area it found, as cards, in the server's order
  const groups = groupByArea(shown);
  const folding = filters.problemFirst && isUnfiltered(filters);
  const needing = folding ? groups.filter(hasProblem) : groups;
  const quiet = folding ? groups.filter((group) => !hasProblem(group)) : [];

  return {
    data, isLoading, error, shown, needing, quiet, showAllQuiet,
    quietShown: showAllQuiet ? quiet : quiet.slice(0, QUIET_ROWS),
    clearFilters: () => dispatch(filtersCleared()),
    toggleAllQuiet: () => setShowAllQuiet((all) => !all),
    retry: () => void refetch(),
  };
}

export function useQuietArea({ group }: AreaSectionProps) {
  const names = useBuildingNames();
  const [open, setOpen] = useState(false);
  const counts = statusCounts(group.points);
  const summary = (Object.keys(counts) as (keyof typeof counts)[])
    .filter((status) => counts[status] > 0)
    .map((status) => `${pointStatusLabel[status]} ${counts[status]}`)
    .join(' · ');
  return { names, open, summary, toggle: () => setOpen((on) => !on) };
}
