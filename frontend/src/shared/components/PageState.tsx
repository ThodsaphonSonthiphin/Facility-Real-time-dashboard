import { Inbox, Lock } from 'lucide-react';
import type { ReactNode } from 'react';

/** Long, short and middling bars, so a skeleton reads as lines of text rather than a grid (the design file). */
const barWidths = ['w-full', 'w-7/10', 'w-17/20'] as const;

/** The 16 px turning ring beside "กำลังโหลด…"; it stands still for someone who asked for less motion. */
export function Spinner() {
  return (
    <span
      aria-hidden="true"
      className="inline-block size-4 shrink-0 animate-spin rounded-full border-2 border-border border-t-accent motion-reduce:animate-none"
    />
  );
}

/**
 * A page or a part of one waiting for its answer (DESIGN_SPEC §6): the spinner and its words, `aria-busy`, and grey
 * bars when `lines` is given. Give `lines` only where the bars are as tall as what replaces them, or the page jumps when
 * the answer lands (the e2e layout-shift checks on My Work and D1).
 */
export function PageLoading({ children = 'กำลังโหลด…', lines = 0 }: { children?: ReactNode; lines?: number }) {
  return (
    <div aria-busy="true" className="flex flex-col gap-3">
      <p className="flex items-center gap-2 text-text-2">
        <Spinner />
        {children}
      </p>
      {Array.from({ length: lines }, (_, index) => (
        <div key={index} className={`h-3 rounded-sm bg-skeleton ${barWidths[index % barWidths.length]}`} />
      ))}
    </div>
  );
}

/**
 * Nothing to show (DESIGN_SPEC §6): a grey 32 px icon, what is empty, and when something will appear. `action` is for
 * "ล้างตัวกรอง" when a filter emptied the list.
 */
export function PageEmpty({
  title,
  children,
  icon = <Inbox aria-hidden="true" className="size-8 text-border-control" />,
  action,
}: {
  title: ReactNode;
  children?: ReactNode;
  /** In place of the inbox; 32 px, `text-border-control`, `aria-hidden`. */
  icon?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-2 px-5 py-8 text-center">
      {icon}
      <p className="font-semibold">{title}</p>
      {children && <p className="text-text-2">{children}</p>}
      {action}
    </div>
  );
}

/** Something turned off (DESIGN_SPEC §6): a grey box with a lock, saying what that means and how to turn it on again. */
export function PageDisabled({ children }: { children: ReactNode }) {
  return (
    <div className="flex items-start gap-2 rounded-md bg-idle-bg p-3 text-idle">
      <Lock aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
      <p>{children}</p>
    </div>
  );
}
