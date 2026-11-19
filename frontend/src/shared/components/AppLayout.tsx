import { ChevronDown, ChevronLeft, LogOut } from 'lucide-react';
import { Link, Outlet } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { AdminNav } from './AdminNav';
import { ProjectLogo } from './ProjectLogo';
import { focusRing } from './styles';

/**
 * The frame of every logged-in page (DESIGN_SPEC §4, #121). No text size here: each page sets its own on its root
 * (text-base on the phone, text-sm on the Admin pages). Printing drops the frame, its width and its padding.
 *
 * `wide` is the Admin's desk (wireframes D1–D11): the menu in a 240 px sidebar from 1024 px, above the page on a
 * narrower screen, and the page in the middle, at most 1216 px with 24 px around it. Without it the page is the phone
 * column (M0–M17): the page, which scrolls with the window. There is no bar shared by the phone pages: each carries its
 * own header (`PhoneScreen`), and ออกจากระบบ is at the end of My Work, My Building and the Admin's sign capture (owner,
 * 2026-10-29, #123 and #124).
 */
export function AppLayout({ wide = false }: { wide?: boolean }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();
  const logOut = () => void endSession(dispatch);

  if (wide) {
    if (user?.role === 'supervisor') {
      return (
        <div className="mx-auto flex min-h-svh w-full max-w-content flex-col gap-4 p-4 sm:p-6 print:p-0">
          <SkipToContent />
          <header className="flex items-center justify-between gap-3 print:hidden">
            <Link
              to="/my-building"
              className={`inline-flex min-h-12 items-center gap-1 font-semibold text-accent hover:text-accent-hover ${focusRing}`}
            >
              <ChevronLeft aria-hidden="true" className="size-5" />
              ตึกของฉัน
            </Link>
            <details className="relative">
              <summary className={`inline-flex min-h-12 cursor-pointer list-none items-center gap-2 rounded-md border border-border-control bg-surface px-3 text-sm font-semibold ${focusRing}`}>
                <span className="max-w-40 truncate">{user.fullName}</span>
                <ChevronDown aria-hidden="true" className="size-4" />
              </summary>
              <div className="absolute right-0 top-full z-20 mt-2 rounded-lg border border-border bg-surface p-3 shadow-overlay">
                <button
                  type="button"
                  data-tap=""
                  className={`inline-flex min-h-12 cursor-pointer items-center gap-2 border-0 bg-transparent p-0 font-semibold text-accent hover:text-accent-hover ${focusRing}`}
                  onClick={logOut}
                >
                  <LogOut aria-hidden="true" className="size-4" />
                  ออกจากระบบ
                </button>
              </div>
            </details>
          </header>
          <div id="main-content" tabIndex={-1} className="min-w-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
            <Outlet />
          </div>
        </div>
      );
    }

    return (
      <div className="min-h-svh lg:flex print:block">
        <aside className="flex flex-col gap-6 border-b border-border bg-surface px-3 py-5 lg:sticky lg:top-0 lg:h-svh lg:w-sidebar lg:shrink-0 lg:overflow-y-auto lg:border-r lg:border-b-0 print:hidden">
          <SkipToContent />
          <div className="flex items-center justify-between gap-3 lg:contents">
            <ProjectLogo compact />
            <AdminNav
              mobileDisclosure
              footer={
                <div className="flex flex-col items-start gap-2">
                  <span className="font-semibold text-text">{user?.fullName}</span>
                  <button
                    type="button"
                    data-tap=""
                    className={`inline-flex min-h-12 cursor-pointer items-center gap-2 border-0 bg-transparent p-0 font-semibold text-accent hover:text-accent-hover ${focusRing}`}
                    onClick={logOut}
                  >
                    <LogOut aria-hidden="true" className="size-4" />
                    ออกจากระบบ
                  </button>
                </div>
              }
            />
          </div>
        </aside>
        <div className="mx-auto flex w-full max-w-content min-w-0 flex-col gap-6 p-6 print:max-w-none print:p-0">
          <div id="main-content" tabIndex={-1} className="min-w-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
            <Outlet />
          </div>
        </div>
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
