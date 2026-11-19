import { Building2, CalendarDays } from 'lucide-react';
import { NavLink } from 'react-router';
import { focusRing } from '../styles';

const itemBase =
  `flex min-h-12 flex-1 items-center justify-center gap-2 rounded-md px-3 text-sm whitespace-nowrap no-underline transition-colors duration-150 sm:min-h-9 sm:flex-none ${focusRing}`;
const itemActive = 'bg-accent-subtle text-accent font-bold';
const itemIdle = 'font-semibold text-text-3 hover:bg-surface-2';

/**
 * Consistent 2-page switcher for Supervisor surfaces (`/my-building` and `/schedule-management`) (facility-0228).
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
