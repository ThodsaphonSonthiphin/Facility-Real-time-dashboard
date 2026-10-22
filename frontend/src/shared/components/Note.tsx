import type { ReactNode } from 'react';

export type NoteTone = 'info' | 'warn' | 'error';

const tones: Record<NoteTone, string> = {
  info: 'border-info-border bg-info-bg text-info-text',
  warn: 'border-warn-border bg-warn-bg text-warn-text',
  error: 'border-error-border bg-error-bg text-error-text',
};

interface NoteProps {
  tone: NoteTone;
  /** A bold first line. */
  title?: ReactNode;
  /** An icon beside the text; give it `aria-hidden` (facility-0088). */
  icon?: ReactNode;
  children?: ReactNode;
}

/** A message in a box (MASTER.md "Notes"). An error is announced at once (`role="alert"`). */
export function Note({ tone, title, icon, children }: NoteProps) {
  return (
    <div role={tone === 'error' ? 'alert' : undefined} className={`flex items-start gap-2.5 rounded border px-3.5 py-2.5 ${tones[tone]}`}>
      {icon}
      <div className="flex min-w-0 flex-col gap-0.5">
        {title && <strong>{title}</strong>}
        {children}
      </div>
    </div>
  );
}
