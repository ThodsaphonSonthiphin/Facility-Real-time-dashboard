import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useRegenerateQrMutation } from './areasApi';
import { useConfirmationAction } from '../../shared/components/useConfirmationDialog';
export function useSignQr(areaId: number, signId: number) {
  const [done, setDone] = useState(false);
  const [regenerate] = useRegenerateQrMutation();
  const confirmation = useConfirmationAction();
  const navigate = useNavigate();
  const ask = () => {
    setDone(false);
    confirmation.ask(async (print) => {
      await regenerate(signId).unwrap();
      setDone(true);
      if (print) navigate(`/admin/print?area=${areaId}&sign=${signId}`);
    });
  };
  return { done, confirmation, ask };
}
