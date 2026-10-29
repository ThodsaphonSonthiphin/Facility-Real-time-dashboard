import type { ReactNode } from 'react';

/**
 * The phone page's white bar at the bottom of the screen for its one main action, a 56 px full-width button
 * (`buttonClass('primary', 'main')`), DESIGN_SPEC §4 (#121). It stays in view while the page scrolls and ends the
 * page: the negative margins reach the edges of the shell's padding, and the bottom inset keeps the button above a
 * phone's home bar; no token describes either. The phone pages move their main button into it in #123.
 */
export function BottomBar({ children }: { children: ReactNode }) {
  return (
    <div className="sticky bottom-0 -mx-4 mt-auto -mb-[max(24px,env(safe-area-inset-bottom))] flex flex-col gap-2 border-t border-border bg-surface px-4 pt-3 pb-[max(12px,env(safe-area-inset-bottom))] print:hidden">
      {children}
    </div>
  );
}
