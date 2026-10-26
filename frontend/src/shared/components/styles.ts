/**
 * Class strings of the new look (facility-0086, 0087) for the elements a component cannot wrap: a react-router Link
 * that looks like a button, a native input or select, and the status colours shared by more than one component. Every
 * class is written out in full, so Tailwind finds it.
 */

import type { PointStatus } from '../api/types';

/**
 * A Point Status's colour as a background, for StatusChip and for a mark beside the status's words (D1's legend).
 * facility-0087 rule 3: the three statuses with nothing to do share one grey.
 */
export const statusBg: Record<PointStatus, string> = {
  Rework: 'bg-status-rework',
  Overdue: 'bg-status-overdue',
  PendingInspection: 'bg-status-pending',
  Passed: 'bg-status-passed',
  NotYetDone: 'bg-status-idle',
  BeforeFirstRound: 'bg-status-idle',
  OffHours: 'bg-status-idle',
};

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'link';
/** phone: 48 px; main: the phone page's one main action, 56 px and full width; desk: the Admin pages, 36 px. */
export type ButtonSize = 'phone' | 'main' | 'desk';
export type InputSize = 'phone' | 'desk';

/** The keyboard focus ring of every control (MASTER.md: visible focus on every control). */
export const focusRing = 'focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-primary';

const buttonBase = `inline-flex items-center justify-center gap-2 rounded border font-semibold cursor-pointer ${focusRing} disabled:cursor-not-allowed disabled:opacity-50`;

// Blue means "can be pressed" (facility-0087 rule 2). danger confirms what cannot be undone: the error note's red as
// text and border on white, never a status colour (owner, 2026-10-22)
const buttonVariants: Record<ButtonVariant, string> = {
  primary: 'border-primary bg-primary text-on-primary no-underline hover:border-primary-strong hover:bg-primary-strong',
  secondary: 'border-input-border bg-surface text-text no-underline hover:border-primary',
  danger: 'border-error-text bg-surface text-error-text no-underline hover:bg-error-bg',
  link: 'border-transparent bg-transparent text-primary underline hover:text-primary-strong',
};

const buttonSizes: Record<ButtonSize, string> = {
  phone: 'min-h-12 text-base',
  main: 'min-h-14 w-full text-lg',
  desk: 'min-h-9 text-sm',
};

const buttonPadding: Record<ButtonSize, string> = {
  phone: 'px-4',
  main: 'px-5',
  desk: 'px-3',
};

/**
 * A button's classes; a Link styled with them also needs `data-tap` for the press feedback. A link-looking button has
 * no side padding, so its words line up with the text above it, such as a table's column heading (owner, #78).
 */
export function buttonClass(variant: ButtonVariant = 'secondary', size: ButtonSize = 'phone'): string {
  const padding = variant === 'link' ? 'px-0' : buttonPadding[size];
  return `${buttonBase} ${buttonVariants[variant]} ${buttonSizes[size]} ${padding}`;
}

const inputBase = `rounded border border-input-border bg-surface text-text ${focusRing} disabled:opacity-50`;

const inputSizes: Record<InputSize, string> = {
  phone: 'min-h-12 px-3 text-base',
  desk: 'min-h-9 px-2.5 text-sm',
};

/** A text input's or a select's classes. */
export function inputClass(size: InputSize = 'phone'): string {
  return `${inputBase} ${inputSizes[size]}`;
}

const textAreaSizes: Record<InputSize, string> = {
  phone: 'min-h-24 px-3 py-2.5 text-base',
  desk: 'min-h-20 px-2.5 py-2 text-sm',
};

/** A textarea's classes. */
export function textAreaClass(size: InputSize = 'phone'): string {
  return `${inputBase} resize-y ${textAreaSizes[size]}`;
}

/**
 * An Admin page's on/off button (`aria-pressed`): D1's status filters, D2's shift, D3's tabs. 36 px (pages/admin.md);
 * the one that is on gets a blue frame, since blue means "can be pressed". Give it `data-tap` for the press feedback.
 */
export const toggleClass =
  'inline-flex min-h-9 cursor-pointer items-center gap-1.5 rounded border border-input-border bg-surface px-2.5 text-text ' +
  'ring-primary ring-inset enabled:hover:border-primary aria-pressed:border-primary aria-pressed:ring-1 ' +
  `disabled:cursor-not-allowed disabled:opacity-50 ${focusRing}`;

/**
 * An Admin page's view switcher, used with `role="tab"` and `aria-selected` (D4's points and check-in sign): a line
 * under the words rather than a box, so a view switch never looks like the buttons beside it that do something
 * (owner, 2026-10-23). 36 px (pages/admin.md). `-mb-px` lays the 2 px line over the list's 1 px rule.
 */
export const tabListClass = 'flex gap-6 border-b border-border';

export const tabClass =
  'inline-flex min-h-9 -mb-px cursor-pointer items-center gap-1.5 border-x-0 border-t-0 border-b-2 border-transparent ' +
  'bg-transparent px-0.5 pb-2 font-semibold text-muted hover:text-primary ' +
  'aria-selected:border-primary aria-selected:text-primary ' +
  `${focusRing}`;

/** A text link. */
export const linkClass = `text-primary underline hover:text-primary-strong ${focusRing}`;

/**
 * A text link inside a sentence on a phone page: its tap area is 48 px high, but its words and the lines around them stay
 * where they were (the negative margin cancels the padding).
 */
export const inlineLinkClass = `${linkClass} inline-block py-4 -my-4`;

export type NoteTone = 'info' | 'warn' | 'error';

/** A note's border, background and text colours, for a note box and for a chip in a note's colours (D3's alert kinds). */
export const noteTones: Record<NoteTone, string> = {
  info: 'border-info-border bg-info-bg text-info-text',
  warn: 'border-warn-border bg-warn-bg text-warn-text',
  error: 'border-error-border bg-error-bg text-error-text',
};

/** A note box's frame and colours, for Note and for a box that must be a `<section>` with its own heading. */
export function noteClass(tone: NoteTone): string {
  return `rounded border px-3.5 py-2.5 ${noteTones[tone]}`;
}

/** The white box with a thin border that Panel and Facts share. */
export const boxClass = 'rounded border border-border bg-surface px-4 py-3.5';

/** The full-page message of StatusScreen and CrashScreen. */
export const statusScreenClass = {
  main: 'mx-auto flex max-w-phone flex-col gap-3 px-5 py-12 text-center text-base',
  title: 'text-xl font-bold',
};

/** The phone's full-page help of BlockedScanScreen and LocationHelp: what went wrong and what to do next. */
export const helpScreenClass = {
  main: 'flex flex-col gap-4 text-base text-text',
  title: 'text-2xl font-bold',
};
