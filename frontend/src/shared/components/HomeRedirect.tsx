import { Navigate } from 'react-router';
import { useAppSelector } from '../../store/hooks';
import { homeOf } from '../auth/roles';

/** "/" goes to the logged-in account's own page (facility-0058), or to the login page. */
export function HomeRedirect() {
  const user = useAppSelector((state) => state.auth.currentUser);
  return <Navigate to={user ? homeOf(user.role) : '/login'} replace />;
}
