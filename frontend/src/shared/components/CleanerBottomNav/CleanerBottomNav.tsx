import { CalendarDays, ClipboardList, User } from 'lucide-react';
import { NavLink } from 'react-router';
import { focusRing } from '../styles';
import { useCleanerBottomNav } from './useCleanerBottomNav';

const tabBase =
  `flex min-h-14 flex-col items-center justify-center gap-1 rounded-md px-2 py-1.5 text-xs whitespace-nowrap no-underline transition-colors duration-150 ${focusRing}`;
const tabActive = 'bg-accent-subtle text-accent font-bold';
const tabIdle = 'font-semibold text-text-3 hover:bg-surface-2';

/**
 * 3-tab mobile bottom navigation bar for Cleaner screens (`งานของฉัน`, `ตารางของฉัน`, `บัญชี`).
 * Keeps account and logout safely separated from active work lists while staying in thumb reach.
 */
export function CleanerBottomNav() {
  const { isCleaner } = useCleanerBottomNav();

  if (!isCleaner) {
    return null;
  }

  return (
    <div className="flex flex-col bg-surface">
      {/* Safe-area bottom inset keeps tap targets above the phone home bar; no token describes env() */}
      <nav
        aria-label="เมนูหลักแม่บ้าน"
        className="grid grid-cols-3 gap-1 border-t border-border bg-surface px-2 pt-1.5 pb-[max(8px,env(safe-area-inset-bottom))]"
      >
        <NavLink
          to="/my-work"
          data-tap=""
          className={({ isActive }) => `${tabBase} ${isActive ? tabActive : tabIdle}`}
        >
          <ClipboardList aria-hidden="true" className="size-5 shrink-0" />
          <span>งานของฉัน</span>
        </NavLink>
        <NavLink
          to="/my-schedule"
          data-tap=""
          className={({ isActive }) => `${tabBase} ${isActive ? tabActive : tabIdle}`}
        >
          <CalendarDays aria-hidden="true" className="size-5 shrink-0" />
          <span>ตารางของฉัน</span>
        </NavLink>
        <NavLink
          to="/my-account"
          data-tap=""
          className={({ isActive }) => `${tabBase} ${isActive ? tabActive : tabIdle}`}
        >
          <User aria-hidden="true" className="size-5 shrink-0" />
          <span>บัญชี</span>
        </NavLink>
      </nav>
    </div>
  );
}
