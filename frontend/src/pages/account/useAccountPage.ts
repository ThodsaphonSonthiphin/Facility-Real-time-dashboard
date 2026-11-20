import { useState } from 'react';
import { endSession } from '../../shared/auth/endSession';
import { useAppDispatch, useAppSelector } from '../../store/hooks';

export function useAccountPage() {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();
  const [confirmOpen, setConfirmOpen] = useState(false);

  const openConfirm = () => setConfirmOpen(true);
  const closeConfirm = () => setConfirmOpen(false);
  const confirmLogout = () => {
    setConfirmOpen(false);
    void endSession(dispatch);
  };

  return {
    user,
    confirmOpen,
    openConfirm,
    closeConfirm,
    confirmLogout,
  };
}
