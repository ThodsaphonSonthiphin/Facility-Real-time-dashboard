import { Check, Minus } from 'lucide-react';

/**
 * An on/off state as a tag (the design file's D4 and D15): green with a tick when it is on, grey with a minus when it is
 * off. The words always say which, so the colour is never the only sign. 24 px, 4 px corners, 13/600.
 */
export function ActiveTag({
  active,
  on = 'ใช้งานอยู่',
  off = 'ปิดใช้งาน',
}: {
  active: boolean;
  /** What "on" says; D15's table says "ใช้งาน", D4's Area says "ใช้งานอยู่". */
  on?: string;
  off?: string;
}) {
  const tone = active ? 'border-passed-border bg-passed-bg text-passed' : 'border-idle-border bg-idle-bg text-idle';
  const Icon = active ? Check : Minus;
  return (
    <span
      className={`inline-flex min-h-6 items-center gap-1 rounded-sm border px-2 text-xs font-semibold whitespace-nowrap ${tone}`}
    >
      <Icon aria-hidden="true" className="size-4 shrink-0" />
      {active ? on : off}
    </span>
  );
}
