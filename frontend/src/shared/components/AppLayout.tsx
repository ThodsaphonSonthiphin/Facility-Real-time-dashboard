import { Outlet } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { roleLabel } from '../auth/roles';
import { AdminNav } from './AdminNav';
import { Button } from './Button';

// The phone column (480 px) or the Admin desk (1216 px); printing drops the width and the padding. No text size here:
// each page sets its own on its root (text-base on the phone, text-sm on the Admin pages)
const shellClass = {
  phone: 'mx-auto flex max-w-phone flex-col gap-4 px-4 pt-3 pb-6 print:max-w-none print:p-0',
  wide: 'mx-auto flex max-w-desk flex-col gap-4 px-8 pt-4 pb-8 print:max-w-none print:p-0',
};

/**
 * The bar above every logged-in page: who is logged in, and "ออกจากระบบ". `wide` is the Admin's desktop width
 * (wireframes D1–D11) and brings the Admin menu; without it the page is a phone column (M0–M17).
 */
export function AppLayout({ wide = false }: { wide?: boolean }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();

  return (
    <div className={wide ? shellClass.wide : shellClass.phone}>
      <header className="flex items-center justify-between gap-3 text-sm text-muted print:hidden">
        <span className="font-semibold text-text">
          {user?.fullName}
          {user && <span className="font-normal text-muted"> · {roleLabel[user.role]}</span>}
        </span>
        <Button size={wide ? 'desk' : 'phone'} onClick={() => void endSession(dispatch)}>
          ออกจากระบบ
        </Button>
      </header>
      {wide && <AdminNav />}
      <Outlet />
    </div>
  );
}
