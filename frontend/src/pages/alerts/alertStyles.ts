import { boxClass } from '../../shared/components/styles';

/** D3's three boxes: the empty Areas, the alerts and the covers, each a white box with its heading. */
export const sectionClass = `flex flex-col gap-2.5 ${boxClass}`;
export const sectionTitleClass = 'text-base font-bold';

/** A row of D3's Area and cover lists: the words, then what the Admin can do. */
export const rowClass = 'flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border px-3 py-2.5';

export const listClass = 'm-0 flex list-none flex-col gap-2 p-0';
