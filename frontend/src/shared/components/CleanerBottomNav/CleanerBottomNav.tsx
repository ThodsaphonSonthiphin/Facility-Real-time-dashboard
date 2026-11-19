import { CalendarDays, ClipboardList, Clock3, ScanLine } from 'lucide-react';
import { Link } from 'react-router';
import { Note } from '../Note';
import { focusRing } from '../styles';
import { useCleanerBottomNav } from './useCleanerBottomNav';

const tabBase =
  `flex min-h-14 flex-col items-center justify-center gap-1 rounded-md px-1 py-1.5 text-xs font-semibold whitespace-nowrap no-underline transition-colors ${focusRing}`;
const tabActive = 'bg-accent-subtle text-accent';
const tabIdle = 'text-text-3 hover:bg-surface-2';

/**
 * R-01 Option A 4-tab bottom navigation bar for Cleaner mobile pages (`งานของฉัน`, `ลงเวลา`, `สแกน QR`, `ตารางเวร`).
 * Renders only when `user?.role === 'cleaner'`. Never fabricates QR token URLs.
 */
export function CleanerBottomNav() {
  const { isCleaner, activeTab, openGuide, guide, toggleGuide } = useCleanerBottomNav();

  if (!isCleaner) {
    return null;
  }

  return (
    <div className="flex flex-col bg-surface">
      {guide && (
        <div className="border-t border-border px-4 pt-3 pb-2">
          <Note tone="info" status title={guide.title}>
            {guide.detail}
          </Note>
        </div>
      )}
      <nav
        aria-label="เมนูหลักแม่บ้าน"
        className="grid grid-cols-4 gap-1 border-t border-border bg-surface px-2 pt-1.5 pb-[max(8px,env(safe-area-inset-bottom))]"
      >
        <Link
          to="/my-work"
          data-tap=""
          aria-current={activeTab === 'my-work' ? 'page' : undefined}
          className={`${tabBase} ${activeTab === 'my-work' ? tabActive : tabIdle}`}
        >
          <ClipboardList aria-hidden="true" className="size-5 shrink-0" />
          <span>งานของฉัน</span>
        </Link>
        <button
          type="button"
          data-tap=""
          aria-expanded={activeTab === 'check-in' ? undefined : openGuide === 'check-in'}
          aria-current={activeTab === 'check-in' ? 'page' : undefined}
          onClick={() => toggleGuide('check-in')}
          className={`${tabBase} cursor-pointer border-0 bg-transparent ${
            activeTab === 'check-in' || openGuide === 'check-in' ? tabActive : tabIdle
          }`}
        >
          <Clock3 aria-hidden="true" className="size-5 shrink-0" />
          <span>ลงเวลา</span>
        </button>
        <button
          type="button"
          data-tap=""
          aria-expanded={activeTab === 'scan-qr' ? undefined : openGuide === 'scan-qr'}
          aria-current={activeTab === 'scan-qr' ? 'page' : undefined}
          onClick={() => toggleGuide('scan-qr')}
          className={`${tabBase} cursor-pointer border-0 bg-transparent ${
            activeTab === 'scan-qr' || openGuide === 'scan-qr' ? tabActive : tabIdle
          }`}
        >
          <ScanLine aria-hidden="true" className="size-5 shrink-0" />
          <span>สแกน QR</span>
        </button>
        <Link
          to="/my-schedule"
          data-tap=""
          aria-current={activeTab === 'my-schedule' ? 'page' : undefined}
          className={`${tabBase} ${activeTab === 'my-schedule' ? tabActive : tabIdle}`}
        >
          <CalendarDays aria-hidden="true" className="size-5 shrink-0" />
          <span>ตารางเวร</span>
        </Link>
      </nav>
    </div>
  );
}
