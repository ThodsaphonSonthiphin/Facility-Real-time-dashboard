import { useId, useLayoutEffect, useRef, useState, type FocusEvent, type KeyboardEvent, type ReactNode } from 'react';
import { Spinner } from './PageState';
import { boxClass } from './styles';

/** A white box with a thin border, for a group of things on a page. */
export function Panel({ title, children }: { title?: ReactNode; children: ReactNode }) {
  return (
    <section className={`flex flex-col gap-2 ${boxClass}`}>
      {title && <h2 className="text-lg font-semibold">{title}</h2>}
      {children}
    </section>
  );
}

/** Label and value pairs in two columns. */
export function Facts({ items }: { items: readonly { label: ReactNode; value: ReactNode }[] }) {
  return (
    // grid-cols-[auto_1fr]: the labels as wide as the longest one, the values take the rest; no token describes that
    <dl className={`m-0 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1.5 ${boxClass}`}>
      {items.map((item, index) => (
        <div key={index} className="contents">
          <dt className="text-text-2">{item.label}</dt>
          <dd className="m-0 font-semibold">{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * A table that scrolls sideways inside its own box, never the page (facility-0087, `pages/admin.md`). Its cells are
 * styled from here, so pages write plain `<th>` and `<td>`: a 13/600 grey head on surface-2, 14 px rows at least 44 px
 * high, 10/12 padding (the design file's Table). The rules sit in `:where()`, which adds no weight, so a class on one
 * cell (`tableCell`) wins over them. `min-w-180` is 720 px, today's table width.
 */
export function Table({
  label,
  framed = true,
  minWidth = 'min-w-180',
  footer,
  children,
}: {
  label?: string;
  /** Its own white box; turn it off for a table inside a section box that already draws one (D3). */
  framed?: boolean;
  /** Below it the table scrolls sideways: 720 px by default, the design file's D3 tables 560 and 900 px. */
  minWidth?: string;
  /** A bar under the table inside its box, which does not scroll with it (D2's count, legend and pages). */
  footer?: ReactNode;
  children: ReactNode;
}) {
  const table = (
    <HorizontalScrollArea label={label ?? 'ตารางข้อมูล'}>
      <table
        aria-label={label}
        className={
          `w-full ${minWidth} border-collapse text-sm ` +
          '[:where(&_td,&_th)]:border-b [:where(&_td,&_th)]:border-border [:where(&_td,&_th)]:px-3 ' +
          '[:where(&_td,&_th)]:py-2.5 [:where(&_td,&_th)]:text-left [:where(&_td,&_th)]:align-middle ' +
          '[:where(&_td)]:h-11 [:where(&_thead_th)]:bg-surface-2 [:where(&_thead_th)]:text-xs ' +
          '[:where(&_thead_th)]:font-semibold [:where(&_thead_th)]:text-text-2 [:where(&_tbody_tr:last-child>*)]:border-b-0'
        }
      >
        {children}
      </table>
    </HorizontalScrollArea>
  );
  if (!framed && !footer) {
    return table;
  }
  return (
    <div className={framed ? 'overflow-hidden rounded-lg border border-border bg-surface' : undefined}>
      {table}
      {footer && <div className="border-t border-border">{footer}</div>}
    </div>
  );
}

/** A scroll region gains a name and keyboard stop only when its contents extend beyond the available width. */
export function HorizontalScrollArea({
  label,
  children,
  leadingOverlayWidth = 0,
}: {
  label: string;
  children: ReactNode;
  /** Width of a sticky leading column that covers part of the scrollport. */
  leadingOverlayWidth?: number;
}) {
  const scrollArea = useRef<HTMLDivElement>(null);
  const [overflows, setOverflows] = useState(false);
  const [measuredLeadingOverlayWidth, setMeasuredLeadingOverlayWidth] = useState(leadingOverlayWidth);
  const hintId = useId();

  useLayoutEffect(() => {
    const element = scrollArea.current;
    if (!element) return;

    const measureOverflow = () => {
      const next = element.scrollWidth > element.clientWidth + 1;
      setOverflows((current) => (current === next ? current : next));
      if (leadingOverlayWidth) {
        const leadingCell = element.querySelector('thead th:first-child');
        const measuredWidth = leadingCell?.getBoundingClientRect().width;
        const nextLeadingWidth = measuredWidth && measuredWidth > 0 ? measuredWidth : leadingOverlayWidth;
        setMeasuredLeadingOverlayWidth((current) =>
          Math.abs(current - nextLeadingWidth) < 0.5 ? current : nextLeadingWidth,
        );
      }
    };

    measureOverflow();
    const observer = typeof ResizeObserver === 'undefined' ? undefined : new ResizeObserver(measureOverflow);
    observer?.observe(element);
    if (element.firstElementChild) observer?.observe(element.firstElementChild);
    const leadingCell = element.querySelector('thead th:first-child');
    if (leadingCell) observer?.observe(leadingCell);
    window.addEventListener('resize', measureOverflow);
    return () => {
      observer?.disconnect();
      window.removeEventListener('resize', measureOverflow);
    };
  }, [leadingOverlayWidth]);

  const scrollWithKeyboard = (event: KeyboardEvent<HTMLDivElement>) => {
    const element = scrollArea.current;
    if (!overflows || !element || event.target !== element) return;
    const visibleWidth = Math.max(1, element.clientWidth - measuredLeadingOverlayWidth);
    const step = Math.max(40, Math.floor(visibleWidth * 0.8));
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      element.scrollLeft += step;
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      element.scrollLeft -= step;
    } else if (event.key === 'Home') {
      event.preventDefault();
      element.scrollLeft = 0;
    } else if (event.key === 'End') {
      event.preventDefault();
      element.scrollLeft = element.scrollWidth;
    }
  };

  const revealFocusedItem = (event: FocusEvent<HTMLDivElement>) => {
    const element = scrollArea.current;
    const target = event.target;
    if (!element || !measuredLeadingOverlayWidth || target === element || !(target instanceof HTMLElement)) return;

    window.requestAnimationFrame(() => {
      if (!element.isConnected || !target.isConnected) return;
      const areaBounds = element.getBoundingClientRect();
      const targetBounds = target.getBoundingClientRect();
      const unobscuredLeft = areaBounds.left + measuredLeadingOverlayWidth;
      const delta = targetBounds.left < unobscuredLeft
        ? unobscuredLeft - targetBounds.left
        : targetBounds.right > areaBounds.right
          ? areaBounds.right - targetBounds.right
          : 0;
      if (delta) element.scrollLeft -= delta;
    });
  };

  // The scroll padding follows the measured sticky width because long names can exceed the column's Tailwind minimum.
  const scrollPaddingStyle = measuredLeadingOverlayWidth
    ? { scrollPaddingInlineStart: measuredLeadingOverlayWidth }
    : undefined;

  return (
    <div>
      <div
        ref={scrollArea}
        role={overflows ? 'region' : undefined}
        aria-label={overflows ? label : undefined}
        aria-describedby={overflows ? hintId : undefined}
        tabIndex={overflows ? 0 : undefined}
        onKeyDown={scrollWithKeyboard}
        onFocusCapture={revealFocusedItem}
        style={scrollPaddingStyle}
        className="max-w-full overflow-x-auto focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent"
      >
        {children}
      </div>
      {overflows && (
        <p id={hintId} className="px-3 py-2 text-xs text-text-2 sm:sr-only">
          เลื่อนตารางไปด้านข้าง หรือโฟกัสตารางแล้วกดปุ่มลูกศรเพื่อดูข้อมูลเพิ่มเติม
        </p>
      )}
    </div>
  );
}

/**
 * The one line shown while a page waits for its first answer, with the spinner (DESIGN_SPEC §6) and `aria-busy`. No
 * skeleton: what comes next differs from page to page, and the line keeps its old height.
 */
export function Splash({ children }: { children: ReactNode }) {
  return (
    <p aria-busy="true" className="flex items-center justify-center gap-2 px-5 py-12 text-base text-text-2">
      <Spinner />
      {children}
    </p>
  );
}
