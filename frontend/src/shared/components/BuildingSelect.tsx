import { useGetAllBuildingsQuery } from '../api/adminSetupApi';
import { Field, Select } from './Field';

interface BuildingSelectProps {
  value?: number;
  onChange: (buildingId: number | undefined) => void;
  /** List the buildings that are off too, marked (D8). Default: active buildings only (D7). */
  includeOff?: boolean;
}

/**
 * "ตึก" on the report pages (D7, D8): every building, or one by its code. D8 lists the buildings that are off, marked,
 * because their old data can be exported; D7 lists active ones only, because it counts active Supervisors and an off
 * building has none (facility-0090 decision 5, amended). Both read the one list, so the cache is shared.
 */
export function BuildingSelect({ value, onChange, includeOff = false }: BuildingSelectProps) {
  const { data } = useGetAllBuildingsQuery();
  const buildings = (data ?? []).filter((building) => includeOff || building.isActive);
  return (
    <Field label="ตึก">
      <Select
        size="desk"
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))}
      >
        <option value="">ทุกตึก</option>
        {buildings.map((building) => (
          <option key={building.id} value={building.id}>
            {building.isActive ? building.code : `${building.code} (ปิดใช้งาน)`}
          </option>
        ))}
      </Select>
    </Field>
  );
}
