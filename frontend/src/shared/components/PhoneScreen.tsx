import { ChevronLeft } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { useAppSelector } from '../../store/hooks';
import { BottomBar } from './BottomBar';
import { CleanerBottomNav } from './CleanerBottomNav/CleanerBottomNav';
import { SupervisorBottomNav } from './SupervisorNav/SupervisorNav';
import { focusRing } from './styles';

/**
 * Shared white mobile header. Reaches screen edges over shell padding, clears the notch, and supports an optional
 * back link (for focused drill-down flows) plus a right-aligned utility action (e.g. compact Refresh).
 */
export function PhoneHeader({
  back,
  action,
  children,
}: {
  back?: { to: string; label: string };
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <header className="-mx-4 -mt-[max(16px,env(safe-area-inset-top))] flex flex-col gap-1 border-b border-border bg-surface px-4 pt-[max(12px,env(safe-area-inset-top))] pb-3.5">
      {back && (
        <Link
          to={back.to}
          data-tap=""
          className={`-ml-2 inline-flex min-h-12 items-center gap-1 self-start px-2 font-semibold text-accent hover:text-accent-hover ${focusRing}`}
        >
          <ChevronLeft aria-hidden="true" className="size-5" />
          {back.label}
        </Link>
      )}
      {action ? (
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 flex-1 flex-col gap-0.5">{children}</div>
          <div className="shrink-0">{action}</div>
        </div>
      ) : (
        children
      )}
    </header>
  );
}

/**
 * Mobile page frame.
 * - `navMode="auto"` (default): renders the persistent 3-tab bottom bar for Cleaner or Supervisor top-level screens.
 * - `navMode="none"`: Focused Task Mode for QR scan/check-in/inspection/capture flows so bottom CTAs never double-stack.
 */
export function PhoneScreen({
  header,
  bottom,
  navMode = 'auto',
  children,
}: {
  header: ReactNode;
  bottom?: ReactNode;
  navMode?: 'auto' | 'none';
  children: ReactNode;
}) {
  const role = useAppSelector((state) => state.auth.currentUser?.role);
  const showCleanerNav = navMode === 'auto' && role === 'cleaner';
  const showSupervisorNav = navMode === 'auto' && role === 'supervisor';
  const showBottomNav = showCleanerNav || showSupervisorNav;

  return (
    <div className="flex flex-1 flex-col gap-4 text-base text-text">
      {header}
      <main className="flex flex-1 flex-col gap-4">{children}</main>
      {showBottomNav ? (
        <div className="sticky bottom-0 -mx-4 mt-auto -mb-[max(24px,env(safe-area-inset-bottom))] flex flex-col bg-surface print:hidden">
          {bottom && <BottomBar stacked>{bottom}</BottomBar>}
          {showCleanerNav && <CleanerBottomNav />}
          {showSupervisorNav && <SupervisorBottomNav />}
        </div>
      ) : (
        bottom && <BottomBar>{bottom}</BottomBar>
      )}
    </div>
  );
}
