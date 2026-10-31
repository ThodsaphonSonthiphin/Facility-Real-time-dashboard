import { useState } from 'react';
import { useCancelCoverMutation } from '../api/alertsApi';
export function useCoverCancellation(onDone?: (text: string) => void) {
  const [selected, setSelected] = useState<{ id: number; areaCode: string; name: string } | null>(null);
  const [cancelCover, { isLoading }] = useCancelCoverMutation();
  const confirm = async () => {
    if (!selected) return;
    await cancelCover(selected.id).unwrap();
    onDone?.(`ยกเลิกการทำแทนของ ${selected.name} ที่ ${selected.areaCode} แล้ว`);
    setSelected(null);
  };
  return { selected, ask: setSelected, cancel: () => setSelected(null), confirm, isLoading };
}
