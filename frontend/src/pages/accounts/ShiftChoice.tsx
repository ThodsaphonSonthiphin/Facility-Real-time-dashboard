import type { Shift } from '../../shared/api/types';
import { shiftLabel } from '../../shared/utils/labels';

interface ShiftChoiceProps {
  /** The radio group's name, unique per form. */
  name: string;
  value: Shift | '';
  /** Why a shift cannot be picked here, or null. */
  blockOf: (shift: Shift) => string | null;
  disabled: boolean;
  onChange: (shift: Shift) => void;
}

/** D6's "กะ": a held slot or a night in a day-only Area cannot be picked, and the page says why. */
export function ShiftChoice({ name, value, blockOf, disabled, onChange }: ShiftChoiceProps) {
  return (
    <fieldset className="accounts-choice" disabled={disabled}>
      <legend>กะ</legend>
      {(['Day', 'Night'] as const).map((shift) => {
        const block = blockOf(shift);
        return (
          <label key={shift}>
            <input
              type="radio"
              name={name}
              disabled={block !== null}
              checked={value === shift}
              onChange={() => onChange(shift)}
            />
            {` ${shiftLabel[shift]}`}
            {block && <span className="app-muted">{` (${block})`}</span>}
          </label>
        );
      })}
    </fieldset>
  );
}
