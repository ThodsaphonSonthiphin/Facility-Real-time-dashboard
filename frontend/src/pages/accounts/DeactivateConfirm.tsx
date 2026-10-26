import { useState } from 'react';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { noteClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { useSetAccountActiveMutation, type Account } from './accountsApi';

interface DeactivateConfirmProps {
  account: Account;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * Plan 10 decision 7: deactivating asks once, because a Cleaner loses their Area and activation does not give it back
 * (plan 5 decision 3). The devices of the person drop within 5 minutes (facility-0012, 0014). The confirm is `danger`
 * like D4's new QR: the lost Area does not come back.
 */
export function DeactivateConfirm({ account, onDone, onCancel }: DeactivateConfirmProps) {
  const [setAccountActive, { isLoading }] = useSetAccountActiveMutation();
  const [problem, setProblem] = useState<string | null>(null);
  const titleId = `accounts-deactivate-${account.id}`;

  const confirm = async () => {
    try {
      await setAccountActive({ id: account.id, active: false }).unwrap();
      onDone(`ปิดใช้งาน ${account.displayName} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <div
      className={`flex flex-col gap-2 ${noteClass('warn')}`}
      role="alertdialog"
      aria-labelledby={titleId}
      aria-describedby={`${titleId}-detail`}
    >
      <p id={titleId} className="font-bold">{`ปิดใช้งานบัญชี ${account.displayName}?`}</p>
      <div id={`${titleId}-detail`} className="flex flex-col gap-1">
        <p>{`${account.displayName} เข้าสู่ระบบไม่ได้ และทุกเครื่องหลุดภายใน 5 นาที ประวัติยังอยู่ เปิดใช้งานกลับได้`}</p>
        {account.role === 'Cleaner' && account.areaCode && account.shift && (
          <p>{`${account.areaCode} ${shiftShortLabel[account.shift]} จะว่าง เปิดใช้งานกลับแล้วต้องเลือกพื้นที่ใหม่`}</p>
        )}
      </div>
      {problem && <Note tone="error">{problem}</Note>}
      <div className="flex flex-wrap gap-2">
        <Button variant="danger" size="desk" disabled={isLoading} onClick={() => void confirm()}>
          {isLoading ? 'กำลังปิดใช้งาน...' : 'ยืนยัน ปิดใช้งาน'}
        </Button>
        {/* The safe choice has the focus */}
        <Button size="desk" autoFocus disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
      </div>
    </div>
  );
}
