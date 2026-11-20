import { useAppSelector } from '../../../store/hooks';

/**
 * Role-gated 2-tab bottom navigation state for Cleaner mobile routes (facility-0227).
 */
export function useCleanerBottomNav() {
  const isCleaner = useAppSelector((state) => state.auth.currentUser?.role === 'cleaner');
  return { isCleaner };
}
