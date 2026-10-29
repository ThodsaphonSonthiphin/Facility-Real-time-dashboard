import type { ReactNode } from 'react';
import { focusRing } from './styles';

/**
 * desk: the Admin's 34 px row of choices, the chosen one filled blue (the design file's กะ, วัน, ขนาดป้าย). phone: full
 * width and 44 px, the chosen one pale blue with a 3 px line under it, for a phone page that already has a blue main
 * button (DESIGN_SPEC §5). The inset shadow draws that line inside the button; no token describes it.
 */
const sizes = {
  desk: {
    group: 'inline-flex',
    button:
      'min-h-8.5 px-3.5 text-sm first:rounded-l-md last:rounded-r-md aria-pressed:border-accent aria-pressed:bg-accent ' +
      'aria-pressed:text-on-accent',
  },
  phone: {
    group: 'flex w-full',
    button:
      'min-h-11 flex-1 px-2 text-base first:rounded-l-md last:rounded-r-md aria-pressed:bg-accent-subtle ' +
      'aria-pressed:text-accent-hover aria-pressed:shadow-[inset_0_-3px_0_var(--color-accent)]',
  },
} as const;

interface SegmentedProps<T extends string> {
  /** The group's name for a screen reader, such as กะ. */
  label: string;
  options: readonly { value: T; label: ReactNode }[];
  /** The chosen one; none is pressed while it is unknown. */
  value: T | undefined;
  onChange: (value: T) => void;
  disabled?: boolean;
  size?: keyof typeof sizes;
}

/**
 * One choice of a few, side by side in one frame. Each is a button with `aria-pressed`, as in the design file and as
 * before, so its role and name do not change. The buttons share their borders (`-ml-px`), so the frame stays 1 px, and
 * the one with the keyboard is raised so its focus ring is not hidden by the next one.
 */
export function Segmented<T extends string>({
  label,
  options,
  value,
  onChange,
  disabled = false,
  size = 'desk',
}: SegmentedProps<T>) {
  const look = sizes[size];
  return (
    <div role="group" aria-label={label} className={look.group}>
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          data-tap=""
          aria-pressed={value === option.value}
          disabled={disabled}
          onClick={() => onChange(option.value)}
          className={
            'relative -ml-px inline-flex cursor-pointer items-center justify-center border border-border-control bg-surface ' +
            'font-semibold text-text first:ml-0 focus-visible:z-10 disabled:cursor-not-allowed disabled:text-text-2 ' +
            `${focusRing} ${look.button}`
          }
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
