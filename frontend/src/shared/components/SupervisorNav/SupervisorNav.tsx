import { Building2, CalendarDays, User } from 'lucide-react';
import { NavLink } from 'react-router';
import { useAppSelector } from '../../../store/hooks';
import { focusRing } from '../styles';

const itemBase =
  `flex min-h-12 flex-1 items-center justify-center gap-2 rounded-md px-3 text-sm whitespace-nowrap no-underline transition-colors duration-150 sm:min-h-9 sm:flex-none ${focusRing}`;
const itemActive = 'bg-accent-subtle text-accent font-bold';
const itemIdle = 'font-semibold text-text-3 hover:bg-surface-2';

const bottomTabBase =
  `flex min-h-14 flex-col items-center justify-center gap-1 rounded-md px-2 py-1.5 text-xs whitespace-nowrap no-underline transition-colors duration-150 ${focusRing}`;

/**
 * Desktop top switcher for Supervisor surfaces (`/my-building` and `/schedule-management`).
 */
export function SupervisorNav() {
  return (
    <nav
      aria-label="เมนูหัวหน้างาน"
      className="flex min-w-0 items-center gap-1 rounded-lg border border-border bg-surface p-1 print:hidden"
    >
      <NavLink
        to="/my-building"
        data-tap=""
        className={({ isActive }) => `${itemBase} ${isActive ? itemActive : itemIdle}`}
      >
        <Building2 aria-hidden="true" className="size-5 shrink-0 sm:size-4" />
        <span>ตึกของฉัน</span>
      </NavLink>
      <NavLink
        to="/schedule-management"
        data-tap=""
        className={({ isActive }) => `${itemBase} ${isActive ? itemActive : itemIdle}`}
      >
        <CalendarDays aria-hidden="true" className="size-5 shrink-0 sm:size-4" />
        <span>จัดตารางงาน</span>
      </NavLink>
    </nav>
  );
}

/**
 * 3-tab mobile bottom navigation bar for Supervisor screens (`ตึกของฉัน`, `จัดตารางงาน`, `บัญชี`).
 * Mirrors CleanerBottomNav so mobile navigation placement never jumps between pages.
 */
export function SupervisorBottomNav() {
  const isSupervisor = useAppSelector((state) => state.auth.currentUser?.role === 'supervisor');

  if (!isSupervisor) {
    return null;
  }

  return (
    <div className="flex flex-col bg-surface print:hidden">
      {/* Safe-area bottom inset keeps tap targets above the phone home bar; no token describes env() */}
      <nav
        aria-label="เมนูหลักหัวหน้างาน"
        className="grid grid-cols-3 gap-1 border-t border-border bg-surface px-2 pt-1.5 pb-[max(8px,env(safe-area-inset-bottom))]"
      >
        <NavLink
          to="/my-building"
          data-tap=""
          className={({ isActive }) => `${bottomTabBase} ${isActive ? itemActive : itemIdle}`}
        >
          <Building2 aria-hidden="true" className="size-5 shrink-0" />
          <span>ตึกของฉัน</span>
        </NavLink>
        <NavLink
          to="/schedule-management"
          data-tap=""
          className={({ isActive }) => `${bottomTabBase} ${isActive ? itemActive : itemIdle}`}
        >
          <CalendarDays aria-hidden="true" className="size-5 shrink-0" />
          <span>จัดตารางงาน</span>
        </NavLink>
        <NavLink
          to="/my-account"
          data-tap=""
          className={({ isActive }) => `${bottomTabBase} ${isActive ? itemActive : itemIdle}`}
        >
          <User aria-hidden="true" className="size-5 shrink-0" />
          <span>บัญชี</span>
        </NavLink>
      </nav>
    </div>
  );
}
