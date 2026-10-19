import { NavLink } from 'react-router';
import './shared.css';

/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). Plan 11 adds
 * รายการที่ขึ้นเตือน; plan 12 adds สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด' },
  { to: '/admin/attendance', label: 'การเข้างาน' },
  { to: '/admin/areas', label: 'จัดการ Area และป้าย' },
  { to: '/admin/accounts', label: 'บัญชีผู้ใช้' },
] as const;

export function AdminNav() {
  return (
    <nav className="app-nav" aria-label="เมนู Admin">
      <ul>
        {adminLinks.map((link) => (
          <li key={link.to}>
            <NavLink
              to={link.to}
              className={({ isActive }) => (isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link')}
            >
              {link.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
