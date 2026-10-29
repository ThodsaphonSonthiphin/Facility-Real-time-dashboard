import { ArrowRightLeft, Clock, MapPin, Pencil, Wrench, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

/**
 * The flags that are not a status (DESIGN_SPEC §2): มีปัญหาที่แจ้ง, ไกลป้าย, ส่งช้า n นาที, ทำแทน, แก้โดยแอดมิน.
 * แก้โดยแอดมิน has no icon and a grey background, as in the design file's DesignSystem page.
 */
export type BadgeKind = 'issue' | 'far' | 'late' | 'cover' | 'edited';

const kinds: Record<BadgeKind, { tone: string; Icon?: LucideIcon }> = {
  issue: { tone: 'border-rework-border bg-surface text-rework', Icon: Wrench },
  far: { tone: 'border-overdue-border bg-surface text-overdue', Icon: MapPin },
  late: { tone: 'border-overdue-border bg-surface text-overdue', Icon: Clock },
  cover: {
    tone: 'border-break-border bg-surface text-break',
    Icon: ArrowRightLeft,
  },
  edited: { tone: 'border-idle-border bg-idle-bg text-idle' },
};

/**
 * A flag beside a point or a person: 24 px, 4 px corners, 13/600, white with a pale border, so it is told apart from a
 * StatusChip (28 px, pale background) even right beside one. The caller gives the words, since ส่งช้า carries its
 * minutes; the icon is decoration (`aria-hidden`).
 */
export function Badge({ kind, children }: { kind: BadgeKind; children: ReactNode }) {
  const { tone, Icon } = kinds[kind];
  return (
    <span
      className={`inline-flex min-h-6 items-center gap-1.5 rounded-sm border px-2 text-xs font-semibold whitespace-nowrap ${tone}`}
    >
      {Icon && <Icon aria-hidden="true" className="size-4 shrink-0" />}
      {children}
    </span>
  );
}

/**
 * The small [✎ แก้] before a time an Admin corrected (DESIGN_SPEC §5 Table): 20 px, on the same line as the time. Its
 * `title` and the words read by a screen reader both say แก้โดยแอดมิน.
 */
export function EditedMark() {
  return (
    <span
      title="แก้โดยแอดมิน"
      className="inline-flex min-h-5 items-center gap-0.5 rounded-sm border border-idle-border bg-idle-bg px-1.5 text-xs font-semibold whitespace-nowrap text-idle"
    >
      <Pencil aria-hidden="true" className="size-3 shrink-0" />
      <span aria-hidden="true">แก้</span>
      <span className="sr-only">แก้โดยแอดมิน</span>
    </span>
  );
}
