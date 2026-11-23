import { LogOut } from 'lucide-react';
import type { Role } from '../../shared/api/types';
import { AvatarBadge } from '../../shared/components/AvatarBadge/AvatarBadge';
import { Button } from '../../shared/components/Button';
import { LogoutConfirmDialog } from '../../shared/components/LogoutConfirmDialog/LogoutConfirmDialog';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
import { buildCommit } from '../../shared/version/buildInfo';
import { useAccountPage } from './useAccountPage';

const roleLabels: Record<Role, string> = {
  cleaner: 'แม่บ้าน',
  supervisor: 'หัวหน้างาน',
  admin: 'แอดมิน',
};

/**
 * Dedicated mobile Account tab (`/my-account`) for Cleaners and Supervisors.
 * Houses user identity and confirmed sign-out cleanly separated from active task lists (Heuristics #3, #4, #5).
 */
export function AccountPage() {
  const { user, confirmOpen, openConfirm, closeConfirm, confirmLogout } = useAccountPage();

  return (
    <PhoneScreen
      header={
        <PhoneHeader>
          <h1 className="text-2xl font-bold text-text">บัญชีของฉัน</h1>
        </PhoneHeader>
      }
    >
      {user && (
        <section
          aria-label="ข้อมูลผู้ใช้งาน"
          className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4"
        >
          <div className="flex items-center gap-3">
            <AvatarBadge name={user.fullName} size="md" tone="neutral" />
            <div className="flex min-w-0 flex-1 flex-col gap-1">
              <strong className="text-lg font-bold wrap-break-word text-text">{user.fullName}</strong>
              <span className="inline-flex w-fit items-center rounded-md border border-idle-border bg-idle-bg px-2 py-0.5 text-xs font-semibold text-idle">
                {roleLabels[user.role]}
              </span>
            </div>
          </div>

          <dl className="m-0 grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-2 border-t border-border pt-3 text-base">
            <dt className="text-text-2">รหัสพนักงาน</dt>
            <dd className="m-0 font-mono font-semibold text-text tabular-nums">{user.username}</dd>
            <dt className="text-text-2">บทบาท</dt>
            <dd className="m-0 font-semibold text-text">{roleLabels[user.role]}</dd>
            <dt className="text-text-2">เวอร์ชันระบบ</dt>
            <dd className="m-0 font-mono text-xs text-text-2">{buildCommit}</dd>
          </dl>
        </section>
      )}

      <div className="mt-2 border-t border-border pt-4">
        <Button variant="danger" size="phone" className="w-full" onClick={openConfirm}>
          <LogOut aria-hidden="true" className="size-5" />
          ออกจากระบบ
        </Button>
      </div>

      {confirmOpen && <LogoutConfirmDialog phone onCancel={closeConfirm} onConfirm={confirmLogout} />}
    </PhoneScreen>
  );
}
