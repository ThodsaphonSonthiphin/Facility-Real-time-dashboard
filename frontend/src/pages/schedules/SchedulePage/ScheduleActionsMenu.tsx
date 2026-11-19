import { ArrowRightLeft, Building2, CalendarDays, ChevronDown, UserCheck, UserPlus } from 'lucide-react';
import { useId, useState, type FocusEvent, type KeyboardEvent } from 'react';
import { Button } from '../../../shared/components/Button';
import { DialogHeader, Modal } from '../../../shared/components/Modal';
import { focusRing } from '../../../shared/components/styles';
import type { SchedulePageView } from './type';

export interface ScheduleActionsProps {
  view: SchedulePageView;
  canEditWeekly: boolean;
  canEditRelief: boolean;
  hasDestinations: boolean;
}

interface ActionItem {
  id: string;
  label: string;
  description: string;
  Icon: typeof CalendarDays;
  primary?: boolean;
  accent?: boolean;
  disabled?: boolean;
  onSelect: () => void;
}

function buildActionItems({ view, canEditWeekly, canEditRelief, hasDestinations }: ScheduleActionsProps): ActionItem[] {
  const items: ActionItem[] = [];
  if (canEditWeekly) {
    items.push({
      id: 'weekly',
      label: 'จัดวันหยุดรายสัปดาห์',
      description: 'กำหนดวันหยุดประจำสัปดาห์ให้แม่บ้านประจำ',
      Icon: CalendarDays,
      primary: true,
      onSelect: view.openWeekly,
    });
  }
  if (hasDestinations && canEditRelief) {
    items.push({
      id: 'relief-roster',
      label: 'จัดตารางประจำสแปร์',
      description: 'วางแผนตึกประจำของแม่บ้านสแปร์',
      Icon: Building2,
      onSelect: () => view.openRelief('relief-roster'),
    });
    items.push({
      id: 'relief-assignment',
      label: 'มอบหมายงานสแปร์',
      description: 'ให้สแปร์ลงพื้นที่แทนในวันที่เลือก',
      Icon: UserCheck,
      onSelect: () => view.openRelief('relief-assignment'),
    });
  }
  if (hasDestinations) {
    items.push({
      id: 'relief-loan',
      label: 'บันทึกการยืมสแปร์',
      description: 'บันทึกการยืมสแปร์ข้ามตึก',
      Icon: UserPlus,
      onSelect: () => view.openRelief('relief-loan'),
    });
  }
  if (view.role === 'admin') {
    items.push({
      id: 'cover',
      label: 'มอบหมายทำแทนฉุกเฉิน',
      description: 'ส่งคนเข้าทำแทนเฉพาะกะนี้ทันที',
      Icon: ArrowRightLeft,
      accent: true,
      disabled: !view.coverSlot,
      onSelect: view.openCover,
    });
  }
  return items;
}

/**
 * Mobile M2 pattern: one clean full-width primary button (`จัดการตารางงาน`) that opens a modal sheet listing all
 * schedule management actions with clear descriptions and 48 px touch targets.
 */
export function MobileScheduleActionSheet(props: ScheduleActionsProps) {
  const [open, setOpen] = useState(false);
  const titleId = useId();
  const items = buildActionItems(props);

  if (items.length === 0) {
    return null;
  }

  return (
    <>
      <Button variant="primary" size="phone" className="w-full" onClick={() => setOpen(true)}>
        <CalendarDays aria-hidden="true" className="size-5" />
        <span>จัดการตารางงาน</span>
        <ChevronDown aria-hidden="true" className="size-4" />
      </Button>
      {open && (
        <Modal labelledBy={titleId} onClose={() => setOpen(false)}>
          <DialogHeader id={titleId} title="จัดการตารางงาน" onClose={() => setOpen(false)} />
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {items.map(({ id, label, description, Icon, primary, accent, disabled, onSelect }) => (
              <li key={id}>
                <button
                  type="button"
                  data-tap=""
                  disabled={disabled}
                  onClick={() => {
                    setOpen(false);
                    onSelect();
                  }}
                  className={`flex min-h-14 w-full cursor-pointer items-start gap-3 rounded-lg border px-3.5 py-2.5 text-left transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${focusRing} ${
                    primary
                      ? 'border-accent bg-accent text-on-accent not-disabled:hover:bg-accent-hover'
                      : accent
                        ? 'border-border-control bg-surface text-accent not-disabled:hover:bg-accent-subtle'
                        : 'border-border bg-surface text-text not-disabled:hover:bg-surface-2'
                  }`}
                >
                  <Icon aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
                  <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                    <span className="text-base font-bold">{label}</span>
                    <span className={`text-xs ${primary ? 'text-on-accent/90' : 'text-text-2'}`}>{description}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </Modal>
      )}
    </>
  );
}

/**
 * Desktop Option A pattern: 1 Primary CTA (`จัดวันหยุดรายสัปดาห์`) + 1 overflow menu button (`จัดการเพิ่มเติม`)
 * holding secondary relief and emergency cover actions.
 */
export function DesktopScheduleActions(props: ScheduleActionsProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const items = buildActionItems(props);
  const primaryItem = items.find((item) => item.primary);
  const secondaryItems = items.filter((item) => !item.primary);

  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    const next = event.relatedTarget;
    if (!(next instanceof Node) || !event.currentTarget.contains(next)) {
      setMenuOpen(false);
    }
  };
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && menuOpen) {
      event.preventDefault();
      setMenuOpen(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      {primaryItem && (
        <Button variant="primary" size="desk" onClick={primaryItem.onSelect}>
          <primaryItem.Icon aria-hidden="true" className="size-4" />
          <span>{primaryItem.label}</span>
        </Button>
      )}
      {secondaryItems.length > 0 && (
        <div className="relative" onBlur={onBlur} onKeyDown={onKeyDown}>
          <Button
            variant="secondary"
            size="desk"
            aria-expanded={menuOpen}
            aria-haspopup="menu"
            onClick={() => setMenuOpen((prev) => !prev)}
          >
            <span>จัดการเพิ่มเติม</span>
            <ChevronDown aria-hidden="true" className="size-4" />
          </Button>
          {menuOpen && (
            <div
              role="menu"
              aria-label="คำสั่งจัดการเพิ่มเติม"
              className="absolute right-0 top-full z-30 mt-1.5 w-60 rounded-lg border border-border bg-surface p-1.5 shadow-overlay"
            >
              {secondaryItems.map(({ id, label, Icon, accent, disabled, onSelect }) => (
                <button
                  key={id}
                  type="button"
                  role="menuitem"
                  data-tap=""
                  disabled={disabled}
                  onClick={() => {
                    setMenuOpen(false);
                    onSelect();
                  }}
                  className={`flex min-h-10 w-full cursor-pointer items-center gap-2.5 rounded-md px-3 py-2 text-left text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${focusRing} ${
                    accent
                      ? 'text-accent not-disabled:hover:bg-accent-subtle'
                      : 'text-text not-disabled:hover:bg-surface-2'
                  }`}
                >
                  <Icon aria-hidden="true" className="size-4 shrink-0" />
                  <span>{label}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
