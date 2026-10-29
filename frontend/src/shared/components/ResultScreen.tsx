import type { ReactNode } from 'react';

export type ResultTone = 'ok' | 'warn' | 'err' | 'neu';

/** The band, the 112 px circle and the title take the result's colour (the design file's Result page). */
const tones: Record<ResultTone, { band: string; circle: string; title: string }> = {
  ok: { band: 'bg-passed', circle: 'bg-passed-bg text-passed', title: 'text-passed-strong' },
  warn: { band: 'bg-overdue', circle: 'bg-overdue-bg text-overdue', title: 'text-overdue-strong' },
  err: { band: 'bg-rework', circle: 'bg-rework-bg text-rework', title: 'text-rework-strong' },
  neu: { band: 'bg-idle', circle: 'bg-idle-bg text-idle', title: 'text-text-3' },
};

interface ResultScreenProps {
  tone: ResultTone;
  /** A lucide icon at `size-14` (56 px), `aria-hidden`. */
  icon: ReactNode;
  title: ReactNode;
  /** Under the title: the lines, the notes and the box, centred. */
  children?: ReactNode;
  /** The one main action in the white bar at the bottom (and a second, outlined one, if any); none shows no bar. */
  bottom?: ReactNode;
}

/**
 * A Cleaner's full-screen answer (DESIGN_SPEC §7 Result): a band of the result's colour at the very top, a 112 px
 * circle with a 56 px icon, a 30/700 title in the same colour, then the words, and the next step in the bar at the
 * bottom. Green saved, orange saved with a condition, red not saved, grey an extra. The negative margins cancel the
 * Cleaner shell's padding so the screen is white to its edges; no token describes them.
 */
export function ResultScreen({ tone, icon, title, children, bottom }: ResultScreenProps) {
  const look = tones[tone];
  return (
    <div className="-mx-4 -mt-[max(16px,env(safe-area-inset-top))] -mb-[max(24px,env(safe-area-inset-bottom))] flex flex-1 flex-col bg-surface text-base text-text">
      <div className={`h-[max(8px,env(safe-area-inset-top))] shrink-0 ${look.band}`} />
      <main className="flex flex-1 flex-col items-center gap-4 px-6 pt-10 pb-6 text-center">
        <span className={`mt-4 flex size-28 items-center justify-center rounded-full ${look.circle}`}>{icon}</span>
        <h1 className={`text-3xl font-bold ${look.title}`}>{title}</h1>
        {children}
      </main>
      {bottom && (
        <div className="sticky bottom-0 flex flex-col gap-2 border-t border-border bg-surface px-4 pt-3 pb-[max(12px,env(safe-area-inset-bottom))] print:hidden">
          {bottom}
        </div>
      )}
    </div>
  );
}
