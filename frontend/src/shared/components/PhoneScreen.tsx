import { ChevronLeft } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { BottomBar } from './BottomBar';
import { focusRing } from './styles';

/**
 * A Cleaner phone page's own white header (the design file's Main, CheckIn and Submit; there is no bar shared by the
 * pages). It reaches the screen's edges over the shell's padding and keeps clear of the notch, then draws its line
 * under itself. `back` is the "‹ งานของฉัน" link above the title. The negative top margin cancels the shell's top
 * padding; no token describes either.
 */
export function PhoneHeader({ back, children }: { back?: { to: string; label: string }; children: ReactNode }) {
  return (
    <header className="-mx-4 -mt-[max(16px,env(safe-area-inset-top))] flex flex-col gap-1 border-b border-border bg-surface px-4 pt-[max(12px,env(safe-area-inset-top))] pb-4">
      {back && (
        <Link
          to={back.to}
          data-tap=""
          className={`-ml-2 inline-flex min-h-11 items-center gap-1 self-start px-2 font-semibold text-accent hover:text-accent-hover ${focusRing}`}
        >
          <ChevronLeft aria-hidden="true" className="size-5" />
          {back.label}
        </Link>
      )}
      {children}
    </header>
  );
}

/**
 * The frame of a Cleaner phone page: the header, the page, and the one main action in the white bar at the bottom
 * (`bottom`, DESIGN_SPEC §4). The page is the `<main>`; the bar ends the screen and stays in view while it scrolls.
 */
export function PhoneScreen({
  header,
  bottom,
  children,
}: {
  header: ReactNode;
  bottom?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-1 flex-col gap-4 text-base text-text">
      {header}
      <main className="flex flex-col gap-4">{children}</main>
      {bottom && <BottomBar>{bottom}</BottomBar>}
    </div>
  );
}
