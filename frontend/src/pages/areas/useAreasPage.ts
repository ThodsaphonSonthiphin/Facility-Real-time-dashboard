import { useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { useGetAreaQuery, useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { freshOnOpen } from '../../shared/api/baseApi';

/** Filtering changes only the rail: the route (or original first Area) owns the selected detail. */
export function useAreasPage() {
  const { areaId } = useParams();
  const areas = useGetAreasQuery(undefined, freshOnOpen);
  const names = useBuildingNames();
  const [search, setSearch] = useState('');
  const [buildingId, setBuildingId] = useState('');
  const parsed = areaId === undefined ? areas.data?.[0]?.id : Number(areaId);
  const selectedId = parsed !== undefined && Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
  const detail = useGetAreaQuery(selectedId ?? 0, { ...freshOnOpen, skip: selectedId === undefined });
  const buildings = useMemo(() => [...new Map((areas.data ?? []).map((area) => [area.buildingId, {
    id: area.buildingId,
    code: area.buildingCode,
    name: names.get(area.buildingCode),
  }])).values()].sort((a, b) => a.code.localeCompare(b.code)), [areas.data, names]);
  const filteredAreas = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    return areas.data?.filter((area) =>
      (buildingId === '' || String(area.buildingId) === buildingId) &&
      `${area.code} ${area.name} ${area.buildingCode} ${names.get(area.buildingCode) ?? ''}`.toLocaleLowerCase().includes(query));
  }, [areas.data, buildingId, names, search]);
  const clearFilters = () => { setSearch(''); setBuildingId(''); };
  return {
    areaId, areas, detail, selectedId, names, buildings, filteredAreas, search, setSearch, buildingId, setBuildingId,
    clearFilters, hasFilters: search !== '' || buildingId !== '', error: areas.error ?? detail.error,
  };
}
