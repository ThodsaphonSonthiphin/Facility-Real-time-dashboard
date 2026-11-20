import {
  Building2,
  CalendarDays,
  ChartNoAxesCombined,
  ClipboardCheck,
  Download,
  History,
  LayoutGrid,
  MapPin,
  Menu,
  Printer,
  TriangleAlert,
  User,
  Users,
} from 'lucide-react';
import { useEffect, useId, useRef, useState, type MouseEvent, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router';
import { openAlertCount, useGetAlertsQuery } from '../api/alertListApi';
import { livePoll } from '../api/baseApi';
import { DialogHeader, Drawer } from './Modal';
import { focusRing } from './styles';

/**
 * Admin navigation organized into 3 mental-model groups (Owner approved Option A):
 * 1. หน้างานวันนี้ (live operations)
 * 2. ตารางและรายงาน (scheduling & reporting)
 * 3. ตั้งค่าระบบ (configuration & audit)
 */
const adminGroups = [
  {
    label: 'หน้างานวันนี้',
    links: [
      { to: '/admin/overview', label: 'ภาพรวม', Icon: ChartNoAxesCombined, counted: false },
      { to: '/dashboard', label: 'สถานะจุด', Icon: LayoutGrid, counted: false },
      { to: '/admin/attendance', label: 'การเข้างาน', Icon: Users, counted: false },
      { to: '/admin/alerts', label: 'รายการที่ขึ้นเตือน', Icon: TriangleAlert, counted: true },
    ],
  },
  {
    label: 'ตารางและรายงาน',
    links: [
      { to: '/admin/schedules', label: 'ตารางงาน', Icon: CalendarDays, counted: false },
      { to: '/admin/inspection-summary', label: 'สรุปการตรวจ', Icon: ClipboardCheck, counted: false },
      { to: '/admin/export', label: 'ส่งออก Excel', Icon: Download, counted: false },
    ],
  },
  {
    label: 'ตั้งค่าระบบ',
    links: [
      { to: '/admin/areas', label: 'พื้นที่และป้าย', Icon: MapPin, counted: false },
      { to: '/admin/buildings', label: 'ตึก', Icon: Building2, counted: false },
      { to: '/admin/print', label: 'พิมพ์ป้าย', Icon: Printer, counted: false },
      { to: '/admin/accounts', label: 'บัญชีผู้ใช้', Icon: User, counted: false },
      { to: '/admin/history', label: 'ประวัติการแก้ไข', Icon: History, counted: false },
    ],
  },
] as const;

const linkClass = `flex min-h-12 items-center gap-2.5 rounded-lg px-2.5 text-sm no-underline lg:min-h-10 ${focusRing}`;
const idleClass = 'font-semibold text-text-3 hover:bg-surface-2';
const activeClass = 'bg-accent-subtle font-bold text-accent';

export function AdminNav({
  mobileDisclosure = false,
  footer,
  userName,
}: {
  mobileDisclosure?: boolean;
  footer?: ReactNode;
  userName?: string;
}) {
  const { data: alerts } = useGetAlertsQuery(undefined, { pollingInterval: livePoll.pollingInterval });
  const open = openAlertCount(alerts);
  const drawerTitleId = useId();
  const focusMainAfterNavigation = useRef(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    if (!focusMainAfterNavigation.current) return;
    focusMainAfterNavigation.current = false;
    document.getElementById('main-content')?.focus();
  }, [location.pathname, location.search]);

  const renderMenu = (idPrefix: string) => (
    <nav aria-label="เมนูแอดมิน" className="flex flex-col gap-4 print:hidden">
      {adminGroups.map((group) => (
        <div key={group.label} className="flex flex-col gap-0.5">
          <p id={`${idPrefix}-${group.label}`} className="px-2.5 pb-1 text-xs font-bold text-text-2">
            {group.label}
          </p>
          <ul aria-labelledby={`${idPrefix}-${group.label}`} className="m-0 flex list-none flex-col gap-0.5 p-0">
            {group.links.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  aria-label={link.counted && open > 0 ? `${link.label} ${open > 999 ? `999+ (${open})` : open}` : undefined}
                  className={({ isActive }) => (isActive ? `${linkClass} ${activeClass}` : `${linkClass} ${idleClass}`)}
                >
                  <link.Icon aria-hidden="true" className="size-5 shrink-0 lg:size-4" />
                  <span className="flex-1">{link.label}</span>{link.counted && open > 0 ? ' ' : null}
                  {/* Reserve a 48px count slot across loading, zero, and positive counts so the menu never rewraps. */}
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
    return renderMenu('menu');
  }

  const closeAfterNavigation = (event: MouseEvent<HTMLDivElement>) => {
    const link = (event.target as HTMLElement).closest('a');
    if (!link) return;
    setMobileOpen(false);
    if (link.getAttribute('aria-current') !== 'page') {
      focusMainAfterNavigation.current = true;
    }
  };

  return (
    <div className="min-w-0 shrink-0 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col">
      {/* Mobile Drawer Trigger (< 1024px) */}
      <button
        type="button"
        data-tap=""
        aria-expanded={mobileOpen}
        aria-haspopup="dialog"
        className={`inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-md border border-border-control bg-surface px-3 text-sm font-semibold whitespace-nowrap text-accent lg:hidden ${focusRing}`}
        onClick={() => setMobileOpen(true)}
      >
        <Menu aria-hidden="true" className="size-4" />
        เมนูแอดมิน
        {open > 0 && (
          <span
            aria-hidden="true"
            className="inline-flex h-5 min-w-5 items-center justify-center rounded-md bg-rework px-1 text-xs font-bold text-on-accent tabular-nums"
          >
            {open > 99 ? '99+' : open}
          </span>
        )}
      </button>

      {/* Mobile Full-Height Slide-over Drawer */}
      {mobileOpen && (
        <Drawer labelledBy={drawerTitleId} width="md" onClose={() => setMobileOpen(false)}>
          <DialogHeader
            id={drawerTitleId}
            title="เมนูแอดมิน"
            detail={userName}
            onClose={() => setMobileOpen(false)}
          />
          <div className="flex flex-1 flex-col gap-4" onClick={closeAfterNavigation}>
            {renderMenu('drawer-menu')}
            {footer && (
              <div className="mt-auto border-t border-border pt-4">
                <div className="rounded-lg border border-border bg-surface-2 px-3 py-2.5 text-sm">{footer}</div>
              </div>
            )}
          </div>
        </Drawer>
      )}

      {/* Desktop Persistent Sidebar (>= 1024px) */}
      <div className="hidden lg:mt-4 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col">
        {renderMenu('sidebar-menu')}
        {footer && (
          <div className="mt-auto pt-4">
            <div className="rounded-lg border border-border bg-surface-2 px-3 py-2.5 text-sm">{footer}</div>
          </div>
        )}
      </div>
    </div>
  );
}
