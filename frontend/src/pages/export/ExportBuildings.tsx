import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { focusRing } from '../../shared/components/styles';

interface ExportBuildingsProps {
  buildings: ManagedBuilding[];
  selection: number[] | null;
  selectedCount: number;
  onToggle: (id: number) => void;
  onToggleAll: () => void;
}

export function ExportBuildings({ buildings, selection, selectedCount, onToggle, onToggleAll }: ExportBuildingsProps) {
  return (
    <div className="overflow-hidden rounded-md border border-border">
      <label className="flex min-h-13 cursor-pointer items-center gap-2 border-b border-border bg-surface-2 px-3 py-3">
        <input type="checkbox" className={`size-4.5 shrink-0 accent-accent ${focusRing}`} aria-label="ทุกตึก" checked={selection === null} onChange={onToggleAll} />
        <strong>ทุกตึก</strong>
        <span className="ml-auto text-right text-xs text-text-2">เลือกแล้ว {selectedCount} จาก {buildings.length} ตึก</span>
      </label>
      <div className="grid grid-cols-1 gap-x-4 gap-y-1 p-3 sm:grid-cols-2">
        {buildings.map((building) => (
          <label key={building.id} className="flex min-h-9 min-w-0 cursor-pointer items-center gap-2 py-1.5">
            <input type="checkbox" className={`size-4.5 shrink-0 accent-accent ${focusRing}`} aria-label={`${building.code} · ${building.name}${!building.isActive ? ' (ปิดใช้งาน)' : ''}`} checked={selection === null || selection.includes(building.id)} onChange={() => onToggle(building.id)} />
            <span className="w-16 shrink-0 truncate font-mono text-xs" title={building.code}>{building.code}</span>
            <span className="truncate" title={building.name}>{building.name}{!building.isActive && ' (ปิดใช้งาน)'}</span>
          </label>
        ))}
      </div>
      {buildings.length === 0 && <p className="px-3 pb-3 text-text-2">ยังไม่มีตึกให้เลือก</p>}
    </div>
  );
}
