import { ConfirmationDialog } from '../../shared/components/ConfirmationDialog';
import { shiftShortLabel } from '../../shared/utils/labels';
import type { Account } from './accountsApi';
import { useDeactivateConfirm } from './useDeactivateConfirm';

interface DeactivateConfirmProps {
  account: Account;
  onDone: (text: string) => void;
  onCancel: () => void;
}

export function DeactivateConfirm({ account, onDone, onCancel }: DeactivateConfirmProps) {
  const confirm = useDeactivateConfirm(account, onDone);
  return (
    <ConfirmationDialog
      title={`ปิดใช้งานบัญชี ${account.displayName}?`}
      body={`${account.displayName} เข้าสู่ระบบไม่ได้ และทุกเครื่องหลุดภายใน 5 นาที ประวัติยังอยู่ เปิดใช้งานกลับได้`}
      impacts={account.role === 'Cleaner' && account.areaCode && account.shift
        ? [`${account.areaCode} ${shiftShortLabel[account.shift]} จะว่าง เปิดใช้งานกลับแล้วต้องเลือกพื้นที่ใหม่`]
        : []}
      confirmLabel="ปิดใช้งานบัญชี"
      onCancel={onCancel}
      onConfirm={confirm}
    />
  );
}
