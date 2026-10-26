import type { ReactNode } from 'react';
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
          <dt className="text-muted">{item.label}</dt>
          <dd className="m-0 font-semibold">{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * A table that scrolls sideways inside its own box, never the page (facility-0087, `pages/admin.md`). Its cells are
 * styled from here with `[&_td]`-style variants, so pages write plain `<th>` and `<td>`; `min-w-180` is 720 px,
 * today's table width.
 */
export function Table({ label, children }: { label?: string; children: ReactNode }) {
  return (
    <div className="max-w-full overflow-x-auto rounded border border-border bg-surface">
      <table
        aria-label={label}
        className={
          'w-full min-w-180 border-collapse text-sm ' +
          '[&_td]:border-b [&_td]:border-border [&_td]:px-3 [&_td]:py-2 [&_td]:text-left [&_td]:align-top ' +
          '[&_th]:border-b [&_th]:border-border [&_th]:px-3 [&_th]:py-2 [&_th]:text-left [&_th]:align-top ' +
          '[&_thead_th]:text-xs [&_thead_th]:font-semibold [&_thead_th]:text-muted ' +
          '[&_tbody_tr:last-child>*]:border-b-0'
        }
      >
        {children}
      </table>
    </div>
  );
}

/** The one line shown while a page waits for its first answer. */
export function Splash({ children }: { children: ReactNode }) {
  return <p className="px-5 py-12 text-center text-base text-muted">{children}</p>;
}
