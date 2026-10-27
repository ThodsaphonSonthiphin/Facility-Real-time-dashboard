import { useState } from 'react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardArgs } from '../../shared/api/attendanceBoardApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { toggleClass } from '../../shared/components/styles';
import { shiftShortLabel } from '../../shared/utils/labels';
import { parseShiftDate } from '../../shared/utils/thaiTime';

interface BoardControlsProps {
  board?: AttendanceBoard;
  args: BoardArgs;
  search: string;
  onSearch: (text: string) => void;
  /** Show this shift of this date. */
  onSlot: (shiftDate: string, shift: Shift) => void;
  /** Back to the shift on the clock. */
  onNow: () => void;
  onBuilding: (buildingId: number | undefined) => void;
}

/** Wireframe D2's controls: the shift, a typed date (plan 11 decision 13), the building and the search. */
export function BoardControls({ board, args, search, onSearch, onSlot, onNow, onBuilding }: BoardControlsProps) {
  const { data: buildings } = useGetBuildingsQuery();
  const [dateText, setDateText] = useState('');
  const [dateError, setDateError] = useState<string | null>(null);

  const showDate = () => {
    const date = parseShiftDate(dateText);
    if (!date) {
      setDateError('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
      return;
    }
    setDateError(null);
    onSlot(date, board?.shift ?? 'Day');
  };

  return (
    <div className="flex flex-wrap items-end gap-x-4 gap-y-2">
      <div className="inline-flex gap-1" role="group" aria-label="กะ">
        {(['Day', 'Night'] as const).map((shift) => (
          <button
            key={shift}
            type="button"
            data-tap=""
            className={toggleClass}
            aria-pressed={board?.shift === shift}
            disabled={!board}
            onClick={() => board && onSlot(board.shiftDate, shift)}
          >
            {shiftShortLabel[shift]}
          </button>
        ))}
      </div>
      {/* The date and its buttons are one group, so the eye goes from the date to what it does (owner, #78) */}
      <div className="flex flex-wrap items-end gap-1">
        <Field label="วันที่">
          <TextInput
            size="desk"
            value={dateText}
            placeholder="วว/ดด/ปปปป"
            inputMode="numeric"
            onChange={(event) => setDateText(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                showDate();
              }
            }}
          />
        </Field>
        <Button size="desk" onClick={showDate}>
          ดูวันที่นี้
        </Button>
        {args.shiftDate && (
          <Button size="desk" onClick={onNow}>
            กะปัจจุบัน
          </Button>
        )}
      </div>
      <Field label="ตึก">
        <Select
          size="desk"
          value={args.buildingId ?? ''}
          onChange={(event) => onBuilding(event.target.value === '' ? undefined : Number(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.id}>
              {buildingOptionText(building)}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="ค้นหา">
        <TextInput
          size="desk"
          type="search"
          value={search}
          placeholder="ชื่อพื้นที่หรือแม่บ้าน"
          onChange={(event) => onSearch(event.target.value)}
        />
      </Field>
      {dateError && (
        <div className="basis-full">
          <Note tone="error">{dateError}</Note>
        </div>
      )}
    </div>
  );
}
