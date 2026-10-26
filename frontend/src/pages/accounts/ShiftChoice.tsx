import type { Shift } from '../../shared/api/types';
import { FieldGroup } from '../../shared/components/Field';
import { shiftLabel } from '../../shared/utils/labels';
import { radioClass, radioLabelClass } from './accountStyles';

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
    <FieldGroup legend="กะ" disabled={disabled}>
      {(['Day', 'Night'] as const).map((shift) => {
        const block = blockOf(shift);
        return (
          <label key={shift} className={radioLabelClass}>
            <input
              type="radio"
              className={radioClass}
              name={name}
              disabled={block !== null}
              checked={value === shift}
              onChange={() => onChange(shift)}
            />
            <span>
              {shiftLabel[shift]}
              {block && <span className="text-muted">{` (${block})`}</span>}
            </span>
          </label>
        );
      })}
    </FieldGroup>
  );
}
