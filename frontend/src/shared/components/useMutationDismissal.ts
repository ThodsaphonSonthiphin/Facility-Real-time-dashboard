import { useStore } from 'react-redux';
import type { RootState } from '../../store';
import { useAppSelector } from '../../store/hooks';
import { baseApi } from '../api/baseApi';

/** A Drawer must observe child sends too; read synchronously when Escape arrives before a rerender. */
export function useMutationDismissal(endpoints: readonly string[], onClose: () => void) {
  const store = useStore<RootState>();
  const isPending = (state: RootState) => Object.values(state[baseApi.reducerPath].mutations).some(
    (mutation) => mutation?.status === 'pending' && endpoints.includes(mutation.endpointName ?? ''),
  );
  const pending = useAppSelector(isPending);
  const close = () => {
    if (!isPending(store.getState())) onClose();
  };
  return { pending, close };
}
