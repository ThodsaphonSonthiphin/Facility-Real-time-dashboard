import { CircleCheck, CircleX, Info, RefreshCw, TriangleAlert, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { Button } from './Button';
import { noteClass, type ButtonSize, type NoteTone } from './styles';

export type { NoteTone };

/** Each tone's icon and its colour (the design file's Alert): the words stay dark, the icon carries the tone. */
const toneIcon: Record<NoteTone, { Icon: LucideIcon; colour: string }> = {
  info: { Icon: Info, colour: 'text-pending' },
  success: { Icon: CircleCheck, colour: 'text-passed' },
  warn: { Icon: TriangleAlert, colour: 'text-overdue' },
  error: { Icon: CircleX, colour: 'text-rework' },
};

interface NoteProps {
  tone: NoteTone;
  /** A bold first line. */
  title?: ReactNode;
  /**
   * An icon beside the text, in place of the tone's own; give it `aria-hidden` (facility-0088). `null` shows none.
   */
  icon?: ReactNode;
  children?: ReactNode;
  /**
   * Announce it at once (`role="alert"`): on by default for an error. Turn it off for a red box that is part of the
   * page from the start, such as the Supervisor's defect, so it is not read out as news and is not an alert.
   */
  alert?: boolean;
  /** Announce it politely (`role="status"`) instead: the answer to a press, such as a recorded time. */
  status?: boolean;
  /** Shows "ลองอีกครั้ง" under the words, for an error the user can try again (DESIGN_SPEC §5 Alert). */
  onRetry?: () => void;
  /** The retry button's size: `desk` (36 px) on an Admin page, `phone` (48 px) on a phone page. */
  retrySize?: Extract<ButtonSize, 'phone' | 'desk'>;
}

/**
 * A message in a box (the design file's Alert): the tone's 20 px icon, then a bold title and the words. An error is
 * announced at once (`role="alert"`) unless `alert={false}`; `status` makes it `role="status"`.
 */
export function Note({
  tone,
  title,
  icon,
  children,
  alert = tone === 'error',
  status = false,
  onRetry,
  retrySize = 'desk',
}: NoteProps) {
  const role = status ? 'status' : alert ? 'alert' : undefined;
  const { Icon, colour } = toneIcon[tone];
  return (
    <div role={role} className={`flex items-start gap-3 ${noteClass(tone)}`}>
      {icon === undefined ? <Icon aria-hidden="true" className={`mt-0.5 size-5 shrink-0 ${colour}`} /> : icon}
      <div className="flex min-w-0 flex-col gap-0.5">
        {title && <strong>{title}</strong>}
        {children}
        {onRetry && (
          <Button variant="secondary" size={retrySize} className="mt-2 self-start" onClick={onRetry}>
            <RefreshCw aria-hidden="true" className="size-4" />
            ลองอีกครั้ง
          </Button>
        )}
      </div>
    </div>
  );
}
