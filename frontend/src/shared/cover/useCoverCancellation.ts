import { useState } from 'react';
import { useCancelCoverMutation } from './coverApi';
export function useCoverCancellation(onDone?: (text: string) => void, beforeWrite?: () => Promise<boolean>) {
  const [selected, setSelected] = useState<{ id: number; areaCode: string; name: string } | null>(null);
  const [cancelCover, { isLoading }] = useCancelCoverMutation();
  const confirm = async () => {
    if (!selected) return;
    if (beforeWrite && !(await beforeWrite())) return;
    await cancelCover(selected.id).unwrap();
    onDone?.(`ยกเลิกการทำแทนของ ${selected.name} ที่ ${selected.areaCode} แล้ว`);
    setSelected(null);
  };
  return { selected, ask: setSelected, cancel: () => setSelected(null), confirm, isLoading };
}
