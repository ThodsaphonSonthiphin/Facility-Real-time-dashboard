import { LogOut } from 'lucide-react';
import { Outlet } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { roleLabel } from '../auth/roles';
import { AdminNav } from './AdminNav';
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
    return (
      <div className="min-h-svh lg:flex print:block">
        <aside className="flex flex-col gap-6 border-b border-border bg-surface px-3 py-5 lg:sticky lg:top-0 lg:h-svh lg:w-sidebar lg:shrink-0 lg:overflow-y-auto lg:border-r lg:border-b-0 print:hidden">
          <div className="flex flex-col px-3">
            <span className="text-base font-bold text-text">ระบบตรวจงานแม่บ้าน</span>
            <span className="text-xs text-text-2">{roleLabel.admin}</span>
          </div>
          <AdminNav />
          <div className="flex flex-col items-start gap-2 border-t border-border px-3 pt-4 text-sm lg:mt-auto">
            <span className="font-semibold text-text">{user?.fullName}</span>
            {/* A button, since it acts; drawn as the mockup's link: accent words and icon, no underline */}
            <button
              type="button"
              data-tap=""
              className={`inline-flex min-h-9 cursor-pointer items-center gap-2 border-0 bg-transparent p-0 font-semibold text-accent hover:text-accent-hover ${focusRing}`}
              onClick={logOut}
            >
              <LogOut aria-hidden="true" className="size-4" />
              ออกจากระบบ
            </button>
          </div>
        </aside>
        <div className="mx-auto flex w-full max-w-content min-w-0 flex-col gap-6 p-6 print:max-w-none print:p-0">
          <Outlet />
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
