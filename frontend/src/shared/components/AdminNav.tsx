import { NavLink } from 'react-router';
import { openAlertCount, useGetAlertsQuery } from '../api/alertListApi';
import { livePoll } from '../api/baseApi';

/**
 * The Admin menu of wireframes D1–D10, listing only the pages that exist (plan 10 decision 2). The order follows
 * the wireframes; plan 12 added ส่งออก Excel, which they reach only from page links.
 */
const adminLinks = [
  { to: '/dashboard', label: 'สถานะจุด', counted: false },
  { to: '/admin/attendance', label: 'การเข้างาน', counted: false },
  { to: '/admin/alerts', label: 'รายการที่ขึ้นเตือน', counted: true },
  { to: '/admin/inspection-summary', label: 'สรุปการตรวจ', counted: false },
  { to: '/admin/export', label: 'ส่งออก Excel', counted: false },
  { to: '/admin/areas', label: 'จัดการพื้นที่และป้าย', counted: false },
  { to: '/admin/accounts', label: 'บัญชีผู้ใช้', counted: false },
  { to: '/admin/history', label: 'ประวัติการแก้ไขของแอดมิน', counted: false },
] as const;

const linkClass =
  'inline-flex min-h-9 items-center rounded px-2.5 text-sm no-underline ' +
  'focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-primary';
const idleClass = 'text-text hover:bg-surface';
const activeClass = 'bg-primary font-semibold text-on-primary';

export function AdminNav() {
  // plan 11 decision 12: the open alerts, asked every 30 s on every Admin page; no count while unknown or none
  const { data: alerts } = useGetAlertsQuery(undefined, { pollingInterval: livePoll.pollingInterval });
  const open = openAlertCount(alerts);

  return (
    <nav aria-label="เมนูแอดมิน" className="print:hidden">
      <ul className="m-0 flex list-none flex-wrap gap-1 border-b border-border p-0 pb-1.5">
        {adminLinks.map((link) => (
          <li key={link.to}>
            <NavLink
              to={link.to}
              className={({ isActive }) => (isActive ? `${linkClass} ${activeClass}` : `${linkClass} ${idleClass}`)}
            >
              {link.counted && open > 0 ? `${link.label} (${open})` : link.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
