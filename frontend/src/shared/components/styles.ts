/**
 * Class strings of the new look (facility-0086, 0087) for the elements a component cannot wrap: a react-router Link
 * that looks like a button, a native input or select, and the status colours shared by more than one component. Every
 * class is written out in full, so Tailwind finds it.
 */

import { Check, Clock, Hourglass, Minus, RotateCcw, type LucideIcon } from 'lucide-react';
import type { PointStatus } from '../api/types';

/** Each status's icon (DESIGN_SPEC §2); the three with nothing to do share the minus, as they share one grey. */
export const statusIcon: Record<PointStatus, LucideIcon> = {
  Rework: RotateCcw,
  Overdue: Clock,
  PendingInspection: Hourglass,
  Passed: Check,
  NotYetDone: Minus,
  BeforeFirstRound: Minus,
  OffHours: Minus,
};

/** A Point Status's colour as text, for a heading that names it (D1's status cards); statusBg is the same as a background. */
export const statusText: Record<PointStatus, string> = {
  Rework: 'text-rework',
  Overdue: 'text-overdue',
  PendingInspection: 'text-pending',
  Passed: 'text-passed',
  NotYetDone: 'text-idle',
  BeforeFirstRound: 'text-idle',
  OffHours: 'text-idle',
};

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'link' | 'add';
/** phone: 48 px; main: the phone page's one main action, 56 px and full width; desk: the Admin pages, 36 px. */
export type ButtonSize = 'phone' | 'main' | 'desk';
/** code: the phone's 56 px field for an employee ID or a phone number, with big digits (the design file's Login). */
export type InputSize = 'phone' | 'desk' | 'code';

/** The keyboard focus ring of every control (MASTER.md: visible focus on every control). */
export const focusRing = 'focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-accent';

// The design file's button (Enterprise Light, DesignSystem): 6 px corners. Hover is written `not-disabled:`, not
// `enabled:`, because a Link styled as a button is never :enabled
const buttonBase = `inline-flex items-center justify-center rounded-md border cursor-pointer ${focusRing} disabled:cursor-not-allowed`;

/** One that cannot be pressed is a grey box with grey words, not a faded one (the design file's กดไม่ได้). */
const greyWhenDisabled = 'disabled:border-border disabled:bg-border disabled:text-text-2';

// Blue means "can be pressed" (facility-0087 rule 2). danger confirms what cannot be undone: the error note's red as
// text and border on white, never a status colour (owner, 2026-10-22). link is the design file's text button: accent
// words and no underline; when it cannot be pressed only its words go grey
const buttonVariants: Record<ButtonVariant, string> = {
  primary: `border-accent bg-accent text-on-accent no-underline not-disabled:hover:border-accent-hover not-disabled:hover:bg-accent-hover ${greyWhenDisabled}`,
  secondary: `border-border-control bg-surface text-text no-underline not-disabled:hover:border-accent ${greyWhenDisabled}`,
  add: `border-dashed border-border-control bg-surface text-accent no-underline not-disabled:hover:border-accent ${greyWhenDisabled}`,
  danger: `border-rework bg-surface text-rework no-underline not-disabled:hover:bg-rework-bg ${greyWhenDisabled}`,
  link: 'border-transparent bg-transparent text-accent no-underline not-disabled:hover:text-accent-hover disabled:text-text-2',
};

// main: the phone's send or check-in button, 18/700 with a 12 px gap to its icon (the design file's Main page)
const buttonSizes: Record<ButtonSize, string> = {
  phone: 'min-h-12 gap-2 text-base font-semibold',
  main: 'min-h-14 w-full gap-3 text-lg font-bold',
  desk: 'min-h-9 gap-2 text-sm font-semibold',
};

const buttonPadding: Record<ButtonSize, string> = {
  phone: 'px-4',
  main: 'px-5',
  desk: 'px-4',
};

/**
 * A button's classes; a Link styled with them also needs `data-tap` for the press feedback. A link-looking button has
 * no side padding, so its words line up with the text above it, such as a table's column heading (owner, #78).
 */
export function buttonClass(variant: ButtonVariant = 'secondary', size: ButtonSize = 'phone'): string {
  const padding = variant === 'link' ? 'px-0' : buttonPadding[size];
  return `${buttonBase} ${buttonVariants[variant]} ${buttonSizes[size]} ${padding}`;
}

// The design file's field: 6 px corners; a wrong answer (`aria-invalid`) has a 2 px red border; one that cannot be used
// is grey with grey words, not faded (DesignSystem, Form)
const inputBase =
  `rounded-md border border-border-control bg-surface text-text ${focusRing} ` +
  'aria-invalid:border-2 aria-invalid:border-rework disabled:border-border disabled:bg-bg disabled:text-text-2';

// 22 px is the design file's digit size for the code field; no token describes it
const inputSizes: Record<InputSize, string> = {
  phone: 'min-h-12 px-3 text-base',
  desk: 'min-h-9 px-3 text-sm',
  code: 'min-h-14 px-4 text-[22px] tabular-nums tracking-wider',
};

/** A text input's or a select's classes. */
export function inputClass(size: InputSize = 'phone'): string {
  return `${inputBase} ${inputSizes[size]}`;
}

const textAreaSizes: Record<InputSize, string> = {
  phone: 'min-h-24 px-3 py-2.5 text-base',
  desk: 'min-h-20 px-3 py-2 text-sm',
  code: 'min-h-24 px-3 py-2.5 text-base',
};

/** A textarea's classes. */
export function textAreaClass(size: InputSize = 'phone'): string {
  return `${inputBase} resize-y ${textAreaSizes[size]}`;
}

/**
 * An Admin page's on/off filter (`aria-pressed`): D1's status filters and มีปัญหาที่แจ้ง, D3's การคุย, the account
 * filter, the date-range shortcuts. 36 px, 6 px corners; the one that is on is pale blue with a 2 px blue frame (the
 * design file's Dashboard), drawn with an inset ring so nothing moves. Give it `data-tap` for the press feedback. A row
 * of a few choices in one frame is `Segmented`, a page's views are `Tabs`.
 */
export const toggleClass =
  'inline-flex min-h-9 cursor-pointer items-center gap-2 rounded-md border border-border-control bg-surface px-3 text-text ' +
  'ring-accent ring-inset enabled:hover:border-accent aria-pressed:border-accent aria-pressed:bg-accent-subtle ' +
  `aria-pressed:ring-1 disabled:cursor-not-allowed disabled:opacity-50 ${focusRing}`;

/** A text link. */
export const linkClass = `text-accent underline hover:text-accent-hover ${focusRing}`;

/**
 * A text link inside a sentence on a phone page: its tap area is 48 px high, but its words and the lines around them stay
 * where they were (the negative margin cancels the padding).
 */
export const inlineLinkClass = `${linkClass} inline-block py-4 -my-4`;

export type NoteTone = 'info' | 'success' | 'warn' | 'error';

/**
 * A tone's border, background and text colours, for a chip in a note's colours (D3's alert kinds) and the Toast.
 * success is the design file's สำเร็จ: Passed's green.
 */
export const noteTones: Record<NoteTone, string> = {
  info: 'border-pending-border bg-pending-bg text-pending',
  success: 'border-passed-border bg-passed-bg text-passed',
  warn: 'border-overdue-border bg-overdue-bg text-overdue',
  error: 'border-rework-border bg-rework-bg text-rework',
};

/**
 * The design file's Alert box: the tone's background and border with dark words; only its icon is in the tone's colour
 * (DesignSystem, Alert). For Note and for a box that must be a `<section>` with its own heading.
 */
const noteBoxTones: Record<NoteTone, string> = {
  info: 'border-pending-border bg-pending-bg text-text',
  success: 'border-passed-border bg-passed-bg text-text',
  warn: 'border-overdue-border bg-overdue-bg text-text',
  error: 'border-rework-border bg-rework-bg text-text',
};

/** A note box's frame and colours: 12/16 padding, 6 px corners. */
export function noteClass(tone: NoteTone): string {
  return `rounded-md border px-4 py-3 ${noteBoxTones[tone]}`;
}

/**
 * The design file's column rules (DESIGN_SPEC §5 Table), for a `<th>` and its `<td>`s. Words stay left (the default).
 * num: numbers and times right, in even-width digits. chip: a 148 px column with its 124 px StatusChip (`fixed`) in the
 * middle. actions: buttons right, the column only as wide as they are; the page lays them in a fixed grid, so each
 * button stays in its place on every row.
 */
export const tableCell = {
  num: 'text-right tabular-nums',
  chip: 'w-37 text-center',
  actions: 'w-px text-right whitespace-nowrap',
} as const;

/** The white box with a thin border that Panel and Facts share. */
export const boxClass = 'rounded-lg border border-border bg-surface px-4 py-3.5';

/** The full-page message of StatusScreen and CrashScreen. */
export const statusScreenClass = {
  main: 'mx-auto flex max-w-mobile flex-col gap-3 px-5 py-12 text-center text-base',
  title: 'text-xl font-bold',
};
