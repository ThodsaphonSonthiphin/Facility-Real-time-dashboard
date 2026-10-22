/**
 * Class strings of the new look (facility-0086, 0087) for the elements a component cannot wrap: a react-router Link
 * that looks like a button, a native input or select. Every class is written out in full, so Tailwind finds it.
 */

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
  phone: 'min-h-12 px-4 text-base',
  main: 'min-h-14 w-full px-5 text-lg',
  desk: 'min-h-9 px-3 text-sm',
};

/** A button's classes; a Link styled with them also needs `data-tap` for the press feedback. */
export function buttonClass(variant: ButtonVariant = 'secondary', size: ButtonSize = 'phone'): string {
  return `${buttonBase} ${buttonVariants[variant]} ${buttonSizes[size]}`;
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

/** A text link (the base layer still colours `a` with the old look until #82). */
export const linkClass = `text-primary underline hover:text-primary-strong ${focusRing}`;

export type NoteTone = 'info' | 'warn' | 'error';

const noteTones: Record<NoteTone, string> = {
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
