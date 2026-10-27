import { useMemo } from 'react';
import { useGetAllBuildingsQuery, type Building } from './adminSetupApi';

/**
 * facility-0091 decision 1: Admin pages show a building as its code (set once, on signs and in Excel) and its current
 * name (the one people use, which the Admin may change). Phones and printed signs keep the code alone.
 */

/** Building code → current name, off buildings included. */
export type BuildingNames = ReadonlyMap<string, string>;

/** Asked again on mount once the last answer is over 30 s old: a rename shows, without one request per row. */
const namesFreshness = { refetchOnMountOrArgChange: 30 } as const;

/** A building picker's option: "A · อาคารผลิต 1". */
export function buildingOptionText(building: Pick<Building, 'code' | 'name'>): string {
  return `${building.code} · ${building.name}`;
}

/** The same from a code alone; just the code while the names load or when the code is unknown. */
export function buildingText(code: string, names?: BuildingNames): string {
  const name = names?.get(code);
  return name === undefined ? code : `${code} · ${name}`;
}

/** Every building's current name, from the list the buildings page and the report pickers share. */
export function useBuildingNames(): BuildingNames {
  const { data } = useGetAllBuildingsQuery(undefined, namesFreshness);
  return useMemo(() => new Map((data ?? []).map((building) => [building.code, building.name] as const)), [data]);
}
