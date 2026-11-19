import { CalendarDays, ClipboardList } from 'lucide-react';
import { NavLink } from 'react-router';
import { focusRing } from '../styles';
import { useCleanerBottomNav } from './useCleanerBottomNav';

const tabBase =
  `flex min-h-14 flex-col items-center justify-center gap-1 rounded-md px-2 py-1.5 text-xs whitespace-nowrap no-underline transition-colors duration-150 ${focusRing}`;
const tabActive = 'bg-accent-subtle text-accent font-bold';
const tabIdle = 'font-semibold text-text-3 hover:bg-surface-2';

/**
 * Simplified 2-tab bottom navigation bar for Cleaner mobile pages (`งานของฉัน`, `ตารางของฉัน`) (facility-0227).
 * Renders only when `user?.role === 'cleaner'`. Never fabricates QR token URLs.
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
        className="grid grid-cols-2 gap-1 border-t border-border bg-surface px-2 pt-1.5 pb-[max(8px,env(safe-area-inset-bottom))]"
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
      </nav>
    </div>
  );
}
