import { useMutationDismissal } from '../../shared/components/useMutationDismissal';
import { useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';

/** These picker lists are fetched only while the Area editing Drawer is mounted. */
export function useAreaEditorDrawer(onClose: () => void) {
  const dismissal = useMutationDismissal(['createArea', 'updateArea', 'setAreaActive', 'setSignRadius', 'setSignLocation', 'regenerateQr'], onClose);
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const cleaners = useGetCleanersQuery(undefined, freshOnOpen);
  return { dismissal, buildings, cleaners, error: buildings.error ?? cleaners.error };
}
