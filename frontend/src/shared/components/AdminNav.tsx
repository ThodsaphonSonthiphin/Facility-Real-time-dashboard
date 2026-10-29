import {
  Building2,
  ClipboardCheck,
  Download,
  History,
  LayoutGrid,
  MapPin,
  Printer,
  TriangleAlert,
  User,
  Users,
} from 'lucide-react';
import { NavLink } from 'react-router';
import { openAlertCount, useGetAlertsQuery } from '../api/alertListApi';
import { livePoll } from '../api/baseApi';
import { focusRing } from './styles';

/**
 * The Admin menu in its two groups (DESIGN_SPEC §4, #121): following the work, then setting it up. Only pages that
 * exist (plan 10 decision 2); พิมพ์ป้าย opens D5 with no Area chosen, as its page allows. Each item has the mockup's icon
 * beside its words (facility-0088).
 */
const adminGroups = [
  {
    label: 'ติดตามงาน',
    links: [
      { to: '/dashboard', label: 'สถานะจุด', Icon: LayoutGrid, counted: false },
      { to: '/admin/attendance', label: 'การเข้างาน', Icon: Users, counted: false },
      { to: '/admin/alerts', label: 'รายการที่ขึ้นเตือน', Icon: TriangleAlert, counted: true },
      { to: '/admin/inspection-summary', label: 'สรุปการตรวจ', Icon: ClipboardCheck, counted: false },
      { to: '/admin/export', label: 'ส่งออก Excel', Icon: Download, counted: false },
    ],
  },
  {
    label: 'ตั้งค่า',
    links: [
      { to: '/admin/areas', label: 'พื้นที่และป้าย', Icon: MapPin, counted: false },
      { to: '/admin/buildings', label: 'ตึก', Icon: Building2, counted: false },
      { to: '/admin/print', label: 'พิมพ์ป้าย', Icon: Printer, counted: false },
      { to: '/admin/accounts', label: 'บัญชีผู้ใช้', Icon: User, counted: false },
      { to: '/admin/history', label: 'ประวัติการแก้ไข', Icon: History, counted: false },
    ],
  },
] as const;

// 40 px items; the chosen one in the accent's light blue, since blue means "can be pressed"
const linkClass = `flex min-h-10 items-center gap-3 rounded-md px-3 text-sm no-underline ${focusRing}`;
const idleClass = 'font-medium text-text-3 hover:bg-surface-2';
const activeClass = 'bg-accent-subtle font-semibold text-accent';

export function AdminNav() {
  // plan 11 decision 12: the open alerts, asked every 30 s on every Admin page; no count while unknown or none
  const { data: alerts } = useGetAlertsQuery(undefined, { pollingInterval: livePoll.pollingInterval });
  const open = openAlertCount(alerts);

  return (
    <nav aria-label="เมนูแอดมิน" className="flex flex-col gap-6 print:hidden">
      {adminGroups.map((group) => (
        <div key={group.label} className="flex flex-col gap-0.5">
          <p id={`menu-${group.label}`} className="px-3 pb-1 text-xs font-semibold text-text-2">
            {group.label}
          </p>
          {/* A row that wraps above the page on a narrow screen, a column in the sidebar from 1024 px */}
          <ul aria-labelledby={`menu-${group.label}`} className="m-0 flex list-none flex-wrap gap-0.5 p-0 lg:flex-col">
            {group.links.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  // The red badge is for the eye; screen readers hear "รายการที่ขึ้นเตือน (4)" as before
                  aria-label={link.counted && open > 0 ? `${link.label} (${open})` : undefined}
                  className={({ isActive }) => (isActive ? `${linkClass} ${activeClass}` : `${linkClass} ${idleClass}`)}
                >
                  <link.Icon aria-hidden="true" className="size-5 shrink-0" />
                  <span className="flex-1">{link.label}</span>
                  {link.counted && open > 0 && (
                    <span
                      aria-hidden="true"
                      className="inline-flex h-5.5 min-w-5.5 items-center justify-center rounded-sm bg-rework px-1.5 text-xs font-bold text-on-accent tabular-nums"
                    >
                      {open}
                    </span>
                  )}
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}
