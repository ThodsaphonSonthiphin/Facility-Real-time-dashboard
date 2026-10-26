import { useGetBuildingsQuery } from '../api/adminSetupApi';
import { Field, Select } from './Field';

interface BuildingSelectProps {
  value?: number;
  onChange: (buildingId: number | undefined) => void;
}

/** "ตึก" on the report pages (D7, D8): every building, or one by its code (BuildingDto). */
export function BuildingSelect({ value, onChange }: BuildingSelectProps) {
  const { data: buildings } = useGetBuildingsQuery();
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
            {building.code}
          </option>
        ))}
      </Select>
    </Field>
  );
}
