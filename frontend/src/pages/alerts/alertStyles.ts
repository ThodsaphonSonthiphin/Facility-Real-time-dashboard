/** D3's three boxes (the design file's Alerts): a white box whose head is its own band, the rows reaching its edges. */
export const panelClass = 'flex min-w-0 flex-col overflow-hidden rounded-lg border border-border bg-surface';
export const panelHeadClass = 'flex flex-wrap items-center justify-between gap-3 border-b border-border p-4';
export const panelTitleClass = 'flex items-center gap-2 text-lg font-semibold';

/**
 * The count beside a box's title or a tab's name: 24 px (22 in a tab), 4 px corners, 13/700, in the colour of what it
 * counts: the Areas with nobody orange, the covers purple, the alerts not talked about red, nothing grey.
 */
export const countTones = {
  overdue: 'border border-overdue-border bg-overdue-bg text-overdue',
  break: 'border border-break-border bg-break-bg text-break',
  rework: 'bg-rework-bg text-rework',
  idle: 'bg-idle-bg text-idle',
} as const;

export const countClass = 'inline-flex min-h-6 min-w-6 items-center justify-center rounded-sm px-2 text-xs font-bold';
