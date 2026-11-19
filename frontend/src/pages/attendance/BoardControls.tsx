import { RotateCcw } from 'lucide-react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardArgs } from '../../shared/api/attendanceBoardApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { SearchBox } from '../../shared/components/SearchBox';
import { Select, TextInput } from '../../shared/components/Field';
import { Segmented } from '../../shared/components/Segmented';
import { FilterToolbar } from '../../shared/components/SurfacePanel';
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
    <FilterToolbar
      label="ตัวกรอง"
      className="[&>div:last-child]:ml-auto"
      actions={
        <>
          {canResetToCurrent && onResetToCurrent && (
            <Button variant="secondary" size="desk" onClick={onResetToCurrent}>
              <RotateCcw aria-hidden="true" className="size-4" />
              กลับกะปัจจุบัน
            </Button>
          )}
          <SearchBox label="ค้นหาพื้นที่หรือแม่บ้าน" value={search} onChange={onSearch} className="w-56 max-w-full" />
        </>
      }
    >
      <Segmented
        label="กะ"
        options={shiftOptions}
        value={board?.shift}
        disabled={!board}
        onChange={(shift) => board && onSlot(args.shiftDate ?? board.shiftDate, shift)}
      />
      <label className="inline-flex items-center gap-2">
        วันที่
        <TextInput
          size="desk"
          type="date"
          value={date}
          disabled={!board}
          onChange={(event) => {
            // A date box gives "" while a part of the date is cleared; only a whole date is shown
            if (/^\d{4}-\d{2}-\d{2}$/.test(event.target.value)) {
              onSlot(event.target.value, args.shift ?? board?.shift ?? 'Day');
            }
          }}
        />
      </label>
      <label className="inline-flex min-w-0 max-w-full items-center gap-2">
        ตึก
        <Select
          size="desk"
          className="w-44 min-w-0"
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
      </label>
    </FilterToolbar>
  );
}
