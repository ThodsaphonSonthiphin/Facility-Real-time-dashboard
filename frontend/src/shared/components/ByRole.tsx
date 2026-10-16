import type { ReactNode } from 'react';
import { useAppSelector } from '../../store/hooks';
import type { Role } from '../api/types';

/**
 * One URL, a page per role (facility-0029 item 1): a point's sign opens the Cleaner's submit form or the Supervisor's
 * inspection. It sits inside a ProtectedRoute that already let only these roles in. A page may not import another
 * page, so the route table names both and this picks one.
 */
export function ByRole({ pages }: { pages: Partial<Record<Role, ReactNode>> }) {
  const role = useAppSelector((state) => state.auth.currentUser?.role);
  return <>{role ? pages[role] : null}</>;
}
