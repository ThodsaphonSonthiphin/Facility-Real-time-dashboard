import type { ReactNode } from 'react';
import { noteClass, type NoteTone } from './styles';

export type { NoteTone };

interface NoteProps {
  tone: NoteTone;
  /** A bold first line. */
  title?: ReactNode;
  /** An icon beside the text; give it `aria-hidden` (facility-0088). */
  icon?: ReactNode;
  children?: ReactNode;
  /**
   * Announce it at once (`role="alert"`): on by default for an error. Turn it off for a red box that is part of the
   * page from the start, such as the Supervisor's defect, so it is not read out as news and is not an alert.
   */
  alert?: boolean;
}

/** A message in a box (MASTER.md "Notes"). An error is announced at once (`role="alert"`) unless `alert={false}`. */
export function Note({ tone, title, icon, children, alert = tone === 'error' }: NoteProps) {
  return (
    <div role={alert ? 'alert' : undefined} className={`flex items-start gap-2.5 ${noteClass(tone)}`}>
      {icon}
      <div className="flex min-w-0 flex-col gap-0.5">
        {title && <strong>{title}</strong>}
        {children}
      </div>
    </div>
  );
}
