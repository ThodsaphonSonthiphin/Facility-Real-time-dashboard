import { useRef, type KeyboardEvent, type ReactNode } from 'react';
import { focusRing } from './styles';

interface TabsProps<T extends string> {
  /** The list's name for a screen reader, such as ส่วนของพื้นที่. */
  label: string;
  tabs: readonly { id: T; label: ReactNode }[];
  value: T;
  onChange: (id: T) => void;
  /**
   * Names the ids: tab `<idPrefix>-tab-<id>` and panel `<idPrefix>-panel-<id>`. The page renders the one panel itself,
   * with `role="tabpanel"`, that `id` and `aria-labelledby` set to the tab's.
   */
  idPrefix: string;
  /** Leave out the list's own rule under the tabs, for a row that draws one across the page (D3's tabs and filter). */
  ruled?: boolean;
}

/**
 * Views of one page (the design file's Tabs): 48 px, a 2 px blue line under the chosen one, which lies over the list's
 * 1 px rule (`-mb-px`). Only the chosen tab is in the Tab order; the arrow keys, Home and End move between tabs and
 * choose at once (WAI-ARIA tabs, automatic activation), as D4's tabs did.
 */
export function Tabs<T extends string>({ label, tabs, value, onChange, idPrefix, ruled = true }: TabsProps<T>) {
  const list = useRef<HTMLDivElement>(null);

  const onKey = (event: KeyboardEvent<HTMLDivElement>) => {
    const at = tabs.findIndex((one) => one.id === value);
    const to =
      event.key === 'ArrowRight'
        ? (at + 1) % tabs.length
        : event.key === 'ArrowLeft'
          ? (at - 1 + tabs.length) % tabs.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? tabs.length - 1
              : -1;
    if (to === -1) return;
    event.preventDefault();
    onChange(tabs[to].id);
    list.current?.querySelector<HTMLElement>(`#${idPrefix}-tab-${tabs[to].id}`)?.focus();
  };

  return (
    <div
      ref={list}
      role="tablist"
      aria-label={label}
      className={ruled ? 'flex gap-1 border-b border-border' : 'flex flex-wrap gap-1'}
      onKeyDown={onKey}
    >
      {tabs.map((one) => (
        <button
          key={one.id}
          id={`${idPrefix}-tab-${one.id}`}
          type="button"
          role="tab"
          data-tap=""
          aria-selected={value === one.id}
          aria-controls={value === one.id ? `${idPrefix}-panel-${one.id}` : undefined}
          tabIndex={value === one.id ? 0 : -1}
          onClick={() => onChange(one.id)}
          className={
            '-mb-px inline-flex min-h-12 cursor-pointer items-center gap-2 border-x-0 border-t-0 border-b-2 ' +
            'border-transparent bg-transparent px-3 text-sm font-semibold text-text-3 hover:text-accent ' +
            `aria-selected:border-accent aria-selected:text-accent ${focusRing}`
          }
        >
          {one.label}
        </button>
      ))}
    </div>
  );
}
