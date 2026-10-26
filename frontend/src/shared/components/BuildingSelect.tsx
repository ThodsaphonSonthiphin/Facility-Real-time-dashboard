import { useGetAllBuildingsQuery } from '../api/adminSetupApi';
import { Field, Select } from './Field';

interface BuildingSelectProps {
  value?: number;
  onChange: (buildingId: number | undefined) => void;
}

/**
 * "ตึก" on the report pages (D7, D8): every building, or one by its code. A building that is off is still listed,
 * marked, because its old data is still there (facility-0090 decision 5).
 */
export function BuildingSelect({ value, onChange }: BuildingSelectProps) {
  const { data: buildings } = useGetAllBuildingsQuery();
  return (
    <Field label="ตึก">
      <Select
        size="desk"
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))}
      >
        <option value="">ทุกตึก</option>
        {(buildings ?? []).map((building) => (
          <option key={building.id} value={building.id}>
            {building.isActive ? building.code : `${building.code} (ปิดใช้งาน)`}
          </option>
        ))}
      </Select>
    </Field>
  );
}
