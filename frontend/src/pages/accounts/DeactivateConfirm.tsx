import { useState } from 'react';
import { Button } from '../../shared/components/Button';
import { Modal } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
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
 * (plan 5 decision 3). The devices of the person drop within 5 minutes (facility-0012, 0014). A popup over the page
 * (owner, 2026-10-26), so the Admin must answer it; the confirm is `danger` like D4's new QR, because the lost Area does
 * not come back. Escape and ยกเลิก close it, but not while the send is in flight.
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
    <Modal
      labelledBy={titleId}
      describedBy={`${titleId}-detail`}
      role="alertdialog"
      onClose={() => !isLoading && onCancel()}
    >
      <div className="flex flex-col gap-3">
        <h2 id={titleId} tabIndex={-1} className="text-lg font-semibold">{`ปิดใช้งานบัญชี ${account.displayName}?`}</h2>
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
          <Button size="desk" disabled={isLoading} onClick={onCancel}>
            ยกเลิก
          </Button>
        </div>
      </div>
    </Modal>
  );
}
