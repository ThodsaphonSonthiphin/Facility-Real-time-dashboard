import {
  Building2,
  CalendarDays,
  ChartNoAxesCombined,
  ClipboardCheck,
  Download,
  History,
  LayoutGrid,
  MapPin,
  Printer,
  TriangleAlert,
  User,
  Users,
  ChevronDown,
  Menu,
} from 'lucide-react';
import { useEffect, useRef, useState, type FocusEvent, type KeyboardEvent, type MouseEvent, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router';
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
      { to: '/admin/overview', label: 'ภาพรวม', Icon: ChartNoAxesCombined, counted: false },
      { to: '/dashboard', label: 'สถานะจุด', Icon: LayoutGrid, counted: false },
      { to: '/admin/attendance', label: 'การเข้างาน', Icon: Users, counted: false },
      // facility-0178: the schedule follows attendance in the group that follows the work
      { to: '/admin/schedules', label: 'ตารางงาน', Icon: CalendarDays, counted: false },
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
const linkClass = `flex min-h-12 items-center gap-2.5 rounded-lg px-2.5 text-sm no-underline lg:min-h-10 ${focusRing}`;
const idleClass = 'font-semibold text-text-3 hover:bg-surface-2';
const activeClass = 'bg-accent-subtle font-bold text-accent';

export function AdminNav({ mobileDisclosure = false, footer }: { mobileDisclosure?: boolean; footer?: ReactNode }) {
  // plan 11 decision 12: the open alerts, asked every 30 s on every Admin page; no count while unknown or none
  const { data: alerts } = useGetAlertsQuery(undefined, { pollingInterval: livePoll.pollingInterval });
  const open = openAlertCount(alerts);
  const menuButton = useRef<HTMLButtonElement>(null);
  const focusMainAfterNavigation = useRef(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    if (!focusMainAfterNavigation.current) return;
    focusMainAfterNavigation.current = false;
    document.getElementById('main-content')?.focus();
  }, [location.pathname, location.search]);

  const menu = (
    <nav aria-label="เมนูแอดมิน" className="flex flex-col gap-4 print:hidden">
      {adminGroups.map((group) => (
        <div key={group.label} className="flex flex-col gap-0.5">
          <p id={`menu-${group.label}`} className="px-2.5 pb-1 text-xs font-bold text-text-2">
            {group.label}
          </p>
          {/* A row that wraps above the page on a narrow screen, a column in the sidebar from 1024 px */}
          <ul aria-labelledby={`menu-${group.label}`} className="m-0 grid list-none grid-cols-2 gap-1 p-0 lg:grid-cols-1">
            {group.links.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  // The red badge is for the eye; screen readers hear "รายการที่ขึ้นเตือน (4)" as before
                  aria-label={link.counted && open > 0 ? `${link.label} (${open})` : undefined}
                  className={({ isActive }) => (isActive ? `${linkClass} ${activeClass}` : `${linkClass} ${idleClass}`)}
                >
                  <link.Icon aria-hidden="true" className="size-5 shrink-0 lg:size-4" />
                  <span className="flex-1">{link.label}</span>
                  {/* Keep the same badge slot while loading, empty and populated, so the menu never rewraps. */}
                  {link.counted && (
                    <span aria-hidden="true" className="inline-flex w-12 shrink-0 justify-end">
                      {open > 0 && (
                        <span
                          title={String(open)}
                          className="inline-flex h-5.5 min-w-5.5 items-center justify-center rounded-md bg-rework px-1.5 text-xs font-bold text-on-accent tabular-nums"
                        >
                          {open > 999 ? '999+' : open}
                        </span>
                      )}
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

  if (!mobileDisclosure) {
    return menu;
  }

  const closeAfterNavigation = (event: MouseEvent<HTMLDivElement>) => {
    const link = (event.target as HTMLElement).closest('a');
    if (!link) return;

    setMobileOpen(false);
    if (link.getAttribute('aria-current') === 'page') {
      menuButton.current?.focus();
      return;
    }
    focusMainAfterNavigation.current = true;
  };
  const closeWhenFocusLeaves = (event: FocusEvent<HTMLDivElement>) => {
    const next = event.relatedTarget;
    if (!(next instanceof Node) || !event.currentTarget.contains(next)) {
      setMobileOpen(false);
    }
  };
  const closeOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && mobileOpen) {
      event.preventDefault();
      setMobileOpen(false);
      menuButton.current?.focus();
    }
  };

  return (
    <div className="relative min-w-0 shrink-0 lg:flex lg:min-h-0 lg:flex-1" onBlur={closeWhenFocusLeaves} onKeyDown={closeOnEscape}>
      <button
        ref={menuButton}
        type="button"
        aria-expanded={mobileOpen}
        aria-controls="admin-navigation-panel"
        className={`inline-flex min-h-12 cursor-pointer items-center gap-2 rounded-md border border-border-control bg-surface px-3 text-sm font-semibold whitespace-nowrap text-accent lg:hidden ${focusRing}`}
        onClick={() => setMobileOpen((open) => !open)}
      >
        <Menu aria-hidden="true" className="size-4" />
        เมนูแอดมิน
        <ChevronDown aria-hidden="true" className="size-4" />
      </button>
      <div
        id="admin-navigation-panel"
        onClick={closeAfterNavigation}
        // The panel is anchored below the small-screen menu button and constrained to the viewport because no width token fits it.
        className={`${mobileOpen ? 'block' : 'hidden'} absolute right-0 top-full z-40 mt-2 max-h-svh w-80 max-w-[calc(100vw-1.5rem)] overflow-y-auto rounded-lg border border-border bg-surface p-3 shadow-overlay lg:static lg:z-auto lg:mt-4 lg:flex lg:min-h-0 lg:w-auto lg:max-w-none lg:flex-1 lg:flex-col lg:overflow-visible lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0 lg:shadow-none`}
      >
        {menu}
        {footer && (
          <div className="mt-4 lg:mt-auto lg:pt-4">
            <div className="rounded-lg border border-border bg-surface-2 px-3 py-2.5 text-sm">{footer}</div>
          </div>
        )}
      </div>
    </div>
  );
}
