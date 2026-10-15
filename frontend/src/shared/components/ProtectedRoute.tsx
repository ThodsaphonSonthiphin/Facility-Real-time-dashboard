import { Navigate, Outlet, useLocation } from 'react-router';
import type { Role } from '../api/types';
import { useAppSelector } from '../../store/hooks';
import { homeOf, roleLabel } from '../auth/roles';
import { StatusScreen } from './StatusScreen';

/**
 * facility-0026: one guard, used as a layout route. It hides pages, it does not protect data: the server's
 * policies do that (facility-0010, 0018).
 */
export function ProtectedRoute({ roles }: { roles: readonly Role[] }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const signedOutByUser = useAppSelector((state) => state.auth.signedOutByUser);
  const location = useLocation();

  if (!user) {
    const loginPath = roles.length === 1 && roles[0] === 'admin' ? '/login/admin' : '/login';
    // Remember the page (a scanned sign's URL above all) unless the person logged out on purpose
    return <Navigate to={loginPath} state={signedOutByUser ? undefined : { from: location }} replace />;
  }
  if (!roles.includes(user.role)) {
    return (
      <StatusScreen
        title="ไม่มีสิทธิ์เปิดหน้านี้"
        detail={`หน้านี้สำหรับ${roles.map((role) => roleLabel[role]).join(' หรือ ')} บัญชีนี้เป็น${roleLabel[user.role]}`}
        linkTo={homeOf(user.role)}
        linkText="กลับหน้าของฉัน"
      />
    );
  }
  return <Outlet />;
}
