import type { ReactNode } from 'react';
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
export function Table({ label, children }: { label?: string; children: ReactNode }) {
  return (
    <div className="max-w-full overflow-x-auto rounded-lg border border-border bg-surface">
      <table
        aria-label={label}
        className={
          'w-full min-w-180 border-collapse text-sm ' +
          '[:where(&_td,&_th)]:border-b [:where(&_td,&_th)]:border-border [:where(&_td,&_th)]:px-3 ' +
          '[:where(&_td,&_th)]:py-2.5 [:where(&_td,&_th)]:text-left [:where(&_td,&_th)]:align-middle ' +
          '[:where(&_td)]:h-11 [:where(&_thead_th)]:bg-surface-2 [:where(&_thead_th)]:text-xs ' +
          '[:where(&_thead_th)]:font-semibold [:where(&_thead_th)]:text-text-2 [:where(&_tbody_tr:last-child>*)]:border-b-0'
        }
      >
        {children}
      </table>
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
