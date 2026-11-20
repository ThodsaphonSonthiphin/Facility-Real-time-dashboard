import { LogOut } from 'lucide-react';
import { useState, useSyncExternalStore } from 'react';
import { Outlet } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { AdminNav } from './AdminNav';
import { LogoutConfirmDialog } from './LogoutConfirmDialog/LogoutConfirmDialog';
import { ProjectLogo } from './ProjectLogo';
import { SupervisorBottomNav, SupervisorNav } from './SupervisorNav/SupervisorNav';
import { focusRing } from './styles';

const narrowScreen = '(max-width: 1023px)';
function isNarrowScreen(): boolean {
  return window.matchMedia?.(narrowScreen).matches ?? false;
}
function subscribeToNarrowScreen(listener: () => void) {
  const query = window.matchMedia?.(narrowScreen);
  query?.addEventListener('change', listener);
  return () => query?.removeEventListener('change', listener);
}

/**
 * Shared frame for every authenticated route:
 * - Admin (`wide` + role admin): 240 px sidebar on desktop, full-height slide-over Drawer on mobile, confirmed logout.
 * - Supervisor (`wide` + role supervisor): top switcher + confirmed logout on desktop, unified 3-tab Bottom Nav on mobile.
 * - Phone (default): 480 px mobile column with safe-area padding.
 */
export function AppLayout({ wide = false }: { wide?: boolean }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();
  const [logoutConfirmOpen, setLogoutConfirmOpen] = useState(false);
  const narrow = useSyncExternalStore(subscribeToNarrowScreen, isNarrowScreen);

  const confirmLogout = () => {
    setLogoutConfirmOpen(false);
    void endSession(dispatch);
  };

  if (wide) {
    if (user?.role === 'supervisor') {
      return (
        <div className="flex min-h-svh flex-col print:block">
          <div className="mx-auto flex w-full max-w-content flex-1 flex-col gap-4 px-4 pt-4 pb-6 sm:p-6 print:p-0">
            <SkipToContent />
            {!narrow && (
              <header className="flex flex-wrap items-center justify-between gap-3 print:hidden">
                <SupervisorNav />
                <div className="flex items-center gap-3 rounded-lg border border-border bg-surface px-3 py-1.5 text-sm">
                  <span className="max-w-48 truncate font-semibold text-text">{user.fullName}</span>
                  <button
                    type="button"
                    data-tap=""
                    className={`inline-flex min-h-9 cursor-pointer items-center gap-1.5 border-0 bg-transparent px-1 font-semibold text-rework hover:text-rework-strong ${focusRing}`}
                    onClick={() => setLogoutConfirmOpen(true)}
                  >
                    <LogOut aria-hidden="true" className="size-4" />
                    ออกจากระบบ
                  </button>
                </div>
              </header>
            )}
            <div id="main-content" tabIndex={-1} className="min-w-0 flex-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
              <Outlet />
            </div>
          </div>
          {narrow && (
            <div className="sticky bottom-0 z-20 print:hidden">
              <SupervisorBottomNav />
            </div>
          )}
          {logoutConfirmOpen && (
            <LogoutConfirmDialog onCancel={() => setLogoutConfirmOpen(false)} onConfirm={confirmLogout} />
          )}
        </div>
      );
    }

    return (
      <div className="min-h-svh lg:flex print:block">
        <aside className="flex flex-col border-b border-border bg-surface px-3 py-3 lg:sticky lg:top-0 lg:h-svh lg:w-sidebar lg:shrink-0 lg:overflow-y-auto lg:border-r lg:border-b-0 lg:py-4 print:hidden">
          <SkipToContent />
          <div className="flex items-center justify-between gap-3 lg:contents">
            <div className="min-w-0 lg:border-b lg:border-border lg:px-2 lg:pt-1 lg:pb-3">
              <ProjectLogo compact />
            </div>
            <AdminNav
              mobileDisclosure
              userName={user?.fullName}
              footer={
                <div className="flex flex-col items-start gap-1">
                  <span className="max-w-full font-bold wrap-break-word text-text">{user?.fullName}</span>
                  <button
                    type="button"
                    data-tap=""
                    className={`inline-flex min-h-11 cursor-pointer items-center gap-1.5 border-0 bg-transparent p-0 font-semibold text-rework hover:text-rework-strong lg:min-h-9 ${focusRing}`}
                    onClick={() => setLogoutConfirmOpen(true)}
                  >
                    <LogOut aria-hidden="true" className="size-4" />
                    ออกจากระบบ
                  </button>
                </div>
              }
            />
          </div>
        </aside>
        <div className="mx-auto flex w-full max-w-content min-w-0 flex-col gap-6 p-4 sm:p-6 print:max-w-none print:p-0">
          <div id="main-content" tabIndex={-1} className="min-w-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
            <Outlet />
          </div>
        </div>
        {logoutConfirmOpen && (
          <LogoutConfirmDialog onCancel={() => setLogoutConfirmOpen(false)} onConfirm={confirmLogout} />
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto flex min-h-svh max-w-mobile flex-col print:max-w-none">
      {/* The top inset keeps a page's own header clear of a phone's notch, the bottom inset keeps the last button above
          the home bar (DESIGN_SPEC §4); no token describes env() */}
      <div className="flex flex-1 flex-col gap-4 px-4 pt-[max(16px,env(safe-area-inset-top))] pb-[max(24px,env(safe-area-inset-bottom))] print:p-0">
        <Outlet />
      </div>
    </div>
  );
}

function SkipToContent() {
  return (
    <a
      href="#main-content"
      className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-surface focus:px-4 focus:py-3 focus:font-semibold focus:text-accent focus-visible:outline-2 focus-visible:outline-accent"
    >
      ข้ามไปเนื้อหาหลัก
    </a>
  );
}
