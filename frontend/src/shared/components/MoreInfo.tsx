import { Info } from 'lucide-react';
import type { ReactNode } from 'react';
import { focusRing } from './styles';

/** desk: the Admin pages, 36 px; phone: 48 px and the phone's icon and text sizes (MASTER.md). */
const sizeClass = {
  desk: { summary: 'min-h-9', icon: 'size-4', body: '' },
  phone: { summary: 'min-h-12 text-md', icon: 'size-5', body: 'text-md' },
} as const;

/**
 * Notes about how a page counts or keeps things, shut until asked (owner, 2026-10-26: a page should not show the
 * wireframe's explanations all the time; 2026-10-27: on every page, the phone ones too). A warning about what the user
 * is about to do, and a field's help, stay in the page; this is for the rest. The native <details>: it opens with the
 * keyboard and is read out as expandable.
 */
export function MoreInfo({
  summary,
  size = 'desk',
  children,
}: {
  summary: string;
  size?: 'desk' | 'phone';
  children: ReactNode;
}) {
  const classes = sizeClass[size];
  return (
    <details className="group max-w-[80ch]">
      <summary
        className={`inline-flex ${classes.summary} cursor-pointer list-none items-center gap-1.5 rounded text-primary hover:text-primary-strong ${focusRing}`}
      >
        <Info aria-hidden="true" className={classes.icon} />
        <span className="underline">{summary}</span>
      </summary>
      <div className={`flex flex-col gap-1.5 pb-1 text-muted ${classes.body}`}>{children}</div>
    </details>
  );
}
