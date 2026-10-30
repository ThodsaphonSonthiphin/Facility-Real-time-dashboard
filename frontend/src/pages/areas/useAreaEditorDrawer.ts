import { useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';

/** These picker lists are fetched only while the Area editing Drawer is mounted. */
export function useAreaEditorDrawer() {
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const cleaners = useGetCleanersQuery(undefined, freshOnOpen);
  return { buildings, cleaners, error: buildings.error ?? cleaners.error };
}
