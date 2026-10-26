import { Info } from 'lucide-react';
import type { ReactNode } from 'react';
import { focusRing } from './styles';

/**
 * Notes about how a page counts or keeps things, shut until the Admin asks (owner, 2026-10-26: a page should not show
 * the wireframe's explanations all the time). A warning about what the Admin is about to do, and a field's help, stay
 * in the page; this is for the rest. The native <details>: it opens with the keyboard and is read out as expandable.
 */
export function MoreInfo({ summary, children }: { summary: string; children: ReactNode }) {
  return (
    <details className="group max-w-[80ch]">
      <summary
        className={`inline-flex min-h-9 cursor-pointer list-none items-center gap-1.5 rounded text-primary hover:text-primary-strong ${focusRing}`}
      >
        <Info aria-hidden="true" className="size-4" />
        <span className="underline">{summary}</span>
      </summary>
      <div className="flex flex-col gap-1.5 pb-1 text-muted">{children}</div>
    </details>
  );
}
