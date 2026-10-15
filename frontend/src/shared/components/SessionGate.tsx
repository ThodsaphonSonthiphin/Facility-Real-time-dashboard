import type { ReactNode } from 'react';
import { useAppSelector } from '../../store/hooks';
import './shared.css';

/** facility-0023 item 4: nothing routes until the page-load refresh answers, so a live session never flashes the login page. */
export function SessionGate({ children }: { children: ReactNode }) {
  const isBootstrapping = useAppSelector((state) => state.auth.isBootstrapping);
  if (isBootstrapping) {
    return <p className="app-splash">กำลังตรวจสอบการเข้าสู่ระบบ...</p>;
  }
  return children;
}
