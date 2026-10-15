import { Outlet } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { roleLabel } from '../auth/roles';
import './shared.css';

/**
 * The bar above every logged-in page: who is logged in, and "ออกจากระบบ". `wide` is the Admin's desktop width
 * (wireframes D1–D11); without it the page is a phone column (M0–M17).
 */
export function AppLayout({ wide = false }: { wide?: boolean }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();

  return (
    <div className={wide ? 'app-shell app-shell--wide' : 'app-shell'}>
      <header className="app-bar">
        <span className="app-bar__who">
          {user?.fullName}
          {user && <span className="app-bar__role"> · {roleLabel[user.role]}</span>}
        </span>
        <button type="button" className="app-button" onClick={() => void endSession(dispatch)}>
          ออกจากระบบ
        </button>
      </header>
      <Outlet />
    </div>
  );
}
