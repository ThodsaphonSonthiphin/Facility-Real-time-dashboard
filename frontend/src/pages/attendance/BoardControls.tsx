import { RotateCcw } from 'lucide-react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardArgs } from '../../shared/api/attendanceBoardApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { SearchBox } from '../../shared/components/SearchBox';
import { Segmented } from '../../shared/components/Segmented';
import { shiftLabel } from '../../shared/utils/labels';

const shiftOptions = (['Day', 'Night'] as const).map((shift) => ({ value: shift, label: shiftLabel[shift] }));

interface BoardControlsProps {
  board?: AttendanceBoard;
  args: BoardArgs;
  search: string;
  canResetToCurrent?: boolean;
  onResetToCurrent?: () => void;
  onSearch: (text: string) => void;
  /** Show this shift of this date. */
  onSlot: (shiftDate: string, shift: Shift) => void;
  onBuilding: (buildingId: number | undefined) => void;
}

export function BoardControls({
  board, args, search, canResetToCurrent = false, onResetToCurrent, onSearch, onSlot, onBuilding,
}: BoardControlsProps) {
  const { data: buildings } = useGetBuildingsQuery();
  const date = board?.shiftDate ?? args.shiftDate ?? '';

  return (
    <section
      role="region"
      aria-label="ตัวกรอง"
      className="grid grid-cols-1 items-end gap-3 rounded-lg border border-border bg-surface p-4 sm:grid-cols-2 lg:flex lg:flex-wrap lg:justify-between"
    >
      <div className="grid grid-cols-2 items-end gap-2.5 sm:contents lg:flex lg:flex-wrap lg:items-end lg:gap-3">
        <div className="col-span-2 flex min-w-0 flex-col gap-1 sm:col-span-1">
          <span className="font-semibold">กะ</span>
          <Segmented
            size="adaptive"
            label="กะ"
            options={shiftOptions}
            value={board?.shift}
            disabled={!board}
            onChange={(shift) => board && onSlot(args.shiftDate ?? board.shiftDate, shift)}
          />
        </div>
        <Field label="วันที่">
          <TextInput
            size="adaptive"
            type="date"
            className="w-full lg:w-auto"
            value={date}
            disabled={!board}
            onChange={(event) => {
              // A date box gives "" while a part of the date is cleared; only a whole date is shown
              if (/^\d{4}-\d{2}-\d{2}$/.test(event.target.value)) {
                onSlot(event.target.value, args.shift ?? board?.shift ?? 'Day');
              }
            }}
          />
        </Field>
        <Field label="ตึก">
          <Select
            size="adaptive"
            className="w-full min-w-0 lg:w-44"
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
      </div>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end lg:flex-wrap">
        {canResetToCurrent && onResetToCurrent && (
          <Button variant="secondary" size="adaptive" className="w-full sm:w-auto" onClick={onResetToCurrent}>
            <RotateCcw aria-hidden="true" className="size-4" />
            กลับกะปัจจุบัน
          </Button>
        )}
        <SearchBox
          size="adaptive"
          label="ค้นหาพื้นที่หรือแม่บ้าน"
          value={search}
          onChange={onSearch}
          className="w-full sm:flex-1 lg:w-60 lg:flex-none"
        />
      </div>
    </section>
  );
}
