import { useState } from 'react';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { useConfirmationAction } from '../../shared/components/useConfirmationDialog';
import type { Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useSetAreaActiveMutation } from './areaFormApi';
export function useAreaActiveSection(detail: AreaDetail) {
  const { area } = detail;
  const [setActive, { isLoading }] = useSetAreaActiveMutation();
  const [notice, setNotice] = useState<Notice | null>(null);
  const confirmation = useConfirmationAction();
  const toggle = async () => {
    await setActive({ areaId: area.id, active: !area.isActive }).unwrap();
    setNotice({ kind: 'ok', text: `${area.isActive ? 'ปิด' : 'เปิด'}ใช้งาน ${area.code} แล้ว` });
  };
  const requestToggle = () => {
    if (area.isActive) confirmation.ask(toggle);
    else void toggle().catch((error) => setNotice({ kind: 'error', text: getErrorMessage(error) }));
  };
  return { isLoading, notice, setNotice, confirmation, requestToggle };
}
