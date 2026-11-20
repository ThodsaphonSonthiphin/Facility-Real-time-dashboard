import { LogOut } from 'lucide-react';
import { useId } from 'react';
import { Button } from '../Button';
import { Modal } from '../Modal';
import { focusRing } from '../styles';
import type { LogoutConfirmDialogProps } from './type';

/**
 * Shared confirmation modal before ending any session (Heuristics #3, #4, #5).
 * Prevents accidental sign-outs across Cleaner, Supervisor, and Admin views.
 */
export function LogoutConfirmDialog({ onCancel, onConfirm }: LogoutConfirmDialogProps) {
  const titleId = useId();
  const bodyId = `${titleId}-body`;

  return (
    <Modal labelledBy={titleId} describedBy={bodyId} role="alertdialog" onClose={onCancel}>
      <div className="flex flex-col gap-5 text-base sm:text-sm">
        <div className="flex items-start gap-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-rework-bg text-rework">
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
          <Button size="desk" className="min-h-12 sm:min-h-9" onClick={onCancel}>
            ยกเลิก
          </Button>
          <button
            type="button"
            data-tap=""
            className={`inline-flex min-h-12 cursor-pointer items-center justify-center gap-1.5 rounded-md border border-transparent bg-rework px-4 text-sm font-semibold text-on-accent transition-[color,background-color,transform] duration-150 not-disabled:hover:bg-rework-strong not-disabled:active:scale-[0.98] motion-reduce:transition-none motion-reduce:active:scale-100 sm:min-h-9 ${focusRing}`}
            onClick={onConfirm}
          >
            <LogOut aria-hidden="true" className="size-4" />
            ยืนยันออกจากระบบ
          </button>
        </div>
      </div>
    </Modal>
  );
}
