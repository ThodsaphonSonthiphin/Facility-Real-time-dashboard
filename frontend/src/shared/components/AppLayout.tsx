import { LogOut } from 'lucide-react';
import { useState, useSyncExternalStore } from 'react';
import { Outlet, useLocation } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { AdminNav } from './AdminNav';
import { LogoutConfirmDialog } from './LogoutConfirmDialog/LogoutConfirmDialog';
import { ProjectLogo } from './ProjectLogo';
import { SupervisorBottomNav, SupervisorNav } from './SupervisorNav/SupervisorNav';
import { focusRing } from './styles';

const narrowSupervisorScreen = '(max-width: 767px)';
function isNarrowSupervisorScreen(): boolean {
  return window.matchMedia?.(narrowSupervisorScreen).matches ?? false;
}
function subscribeToNarrowSupervisorScreen(listener: () => void) {
  const query = window.matchMedia?.(narrowSupervisorScreen);
  query?.addEventListener('change', listener);
  return () => query?.removeEventListener('change', listener);
}

/**
 * Shared frame for every authenticated route:
 * - Admin (`wide` + role admin): 240 px sidebar on desktop, sticky top bar + slide-over Drawer on mobile/tablet, confirmed logout.
 * - Supervisor (`wide` or `wideForSupervisor` + role supervisor): top switcher + confirmed logout on tablet/desktop (>= 768 px), unified 3-tab Bottom Nav on mobile (< 768 px).
 * - Phone (default): 480 px mobile column with safe-area padding.
 */
export function AppLayout({ wide = false, wideForSupervisor = false }: { wide?: boolean; wideForSupervisor?: boolean }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();
  const location = useLocation();
  const [logoutConfirmOpen, setLogoutConfirmOpen] = useState(false);
  const narrowSupervisor = useSyncExternalStore(subscribeToNarrowSupervisorScreen, isNarrowSupervisorScreen);
  const useWideShell = wide || (wideForSupervisor && user?.role === 'supervisor');

  const confirmLogout = () => {
    setLogoutConfirmOpen(false);
    void endSession(dispatch);
  };

  if (useWideShell) {
    if (user?.role === 'supervisor') {
      const showHeaderLogout = location.pathname !== '/my-account';
      return (
        <div className="flex min-h-svh flex-col print:block">
          {!narrowSupervisor && (
            /* Safe-area top inset keeps the sticky header clear of notches; no spacing token describes env(). */
            <header className="sticky top-0 z-30 border-b border-border bg-surface pt-[env(safe-area-inset-top)] print:hidden">
              <div className="mx-auto flex w-full max-w-content flex-wrap items-center justify-between gap-3 px-4 py-2.5 sm:px-6">
                <div className="flex min-w-0 flex-wrap items-center gap-3">
                  <ProjectLogo compact />
                  <SupervisorNav />
                </div>
                <div className="flex items-center gap-3 text-sm">
                  <span className="max-w-48 truncate font-semibold text-text">{user.fullName}</span>
                  {showHeaderLogout && (
                    <button
                      type="button"
                      data-tap=""
                      className={`inline-flex min-h-9 cursor-pointer items-center gap-1.5 border-0 bg-transparent px-1 font-semibold text-rework hover:text-rework-strong ${focusRing}`}
                      onClick={() => setLogoutConfirmOpen(true)}
                    >
                      <LogOut aria-hidden="true" className="size-4" />
                      ออกจากระบบ
                    </button>
                  )}
                </div>
              </div>
            </header>
          )}
          {/* Safe-area top inset applies while the mobile bottom-nav shell has no top header (< md); no token describes env(). */}
          <div className="mx-auto flex w-full max-w-content flex-1 flex-col gap-4 px-4 pt-[max(16px,env(safe-area-inset-top))] pb-6 sm:px-6 md:pt-6 print:p-0">
            <SkipToContent />
            <div id="main-content" tabIndex={-1} className="min-w-0 flex-1 scroll-mt-20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
              <Outlet />
            </div>
          </div>
          {narrowSupervisor && (
            <div className="sticky bottom-0 z-30 print:hidden">
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
        {/* Safe-area top inset clears the status bar/notch on mobile & tablet sticky headers; no token describes env(). */}
        <aside className="sticky top-0 z-30 flex flex-col border-b border-border bg-surface px-3 pt-[max(12px,env(safe-area-inset-top))] pb-3 lg:h-svh lg:w-sidebar lg:shrink-0 lg:overflow-y-auto lg:border-r lg:border-b-0 lg:py-4 print:hidden">
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
          <div id="main-content" tabIndex={-1} className="min-w-0 scroll-mt-20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
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
