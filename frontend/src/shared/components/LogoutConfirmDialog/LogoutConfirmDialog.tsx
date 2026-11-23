import { LogOut } from 'lucide-react';
import { useId } from 'react';
import { Button } from '../Button';
import { Modal } from '../Modal';
import type { LogoutConfirmDialogProps } from './type';

/**
 * Shared confirmation modal before ending any session (Heuristics #3, #4, #5).
 * Prevents accidental sign-outs across Cleaner, Supervisor, and Admin views.
 */
export function LogoutConfirmDialog({ phone = false, onCancel, onConfirm }: LogoutConfirmDialogProps) {
  const titleId = useId();
  const bodyId = `${titleId}-body`;
  const buttonSize = phone ? 'phone' : 'adaptive';

  return (
    <Modal labelledBy={titleId} describedBy={bodyId} role="alertdialog" onClose={onCancel}>
      <div className={`flex flex-col gap-5 ${phone ? 'text-base' : 'text-base sm:text-sm'}`}>
        <div className="flex items-start gap-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-md bg-rework-bg text-rework">
            <LogOut aria-hidden="true" className="size-5" />
          </span>
          <div className="flex min-w-0 flex-1 flex-col gap-1.5">
            <h2 id={titleId} tabIndex={-1} className="text-lg font-bold text-text">
              ยืนยันออกจากระบบ
            </h2>
            <p id={bodyId} className="text-text-2">
              ต้องการออกจากระบบใช่ไหม? คุณจะต้องเข้าสู่ระบบใหม่อีกครั้งก่อนใช้งาน
            </p>
          </div>
        </div>
        <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-4">
          <Button size={buttonSize} onClick={onCancel}>
            ยกเลิก
          </Button>
          <Button variant="dangerSolid" size={buttonSize} onClick={onConfirm}>
            <LogOut aria-hidden="true" className={phone ? 'size-5' : 'size-5 sm:size-4'} />
            ยืนยันออกจากระบบ
          </Button>
        </div>
      </div>
    </Modal>
  );
}
