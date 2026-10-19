import { NavLink } from 'react-router';
import { openAlertCount, useGetAlertsQuery } from '../api/alertListApi';
import { livePoll } from '../api/baseApi';
import './shared.css';

/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). Plan 12 adds
 * สรุปการตรวจ, ส่งออก Excel and ประวัติ Admin with their pages.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด', counted: false },
  { to: '/admin/attendance', label: 'การเข้างาน', counted: false },
  { to: '/admin/alerts', label: 'รายการที่ขึ้นเตือน', counted: true },
  { to: '/admin/areas', label: 'จัดการ Area และป้าย', counted: false },
  { to: '/admin/accounts', label: 'บัญชีผู้ใช้', counted: false },
] as const;

export function AdminNav() {
  // plan 11 decision 12: the open alerts, asked every 30 s on every Admin page; no count while unknown or none
  const { data: alerts } = useGetAlertsQuery(undefined, { pollingInterval: livePoll.pollingInterval });
  const open = openAlertCount(alerts);

  return (
    <nav className="app-nav" aria-label="เมนู Admin">
      <ul>
        {adminLinks.map((link) => (
          <li key={link.to}>
            <NavLink
              to={link.to}
              className={({ isActive }) => (isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link')}
            >
              {link.counted && open > 0 ? `${link.label} (${open})` : link.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
