import { Search } from 'lucide-react';
import { Link } from 'react-router';
import type { AreaSummary } from '../../shared/api/adminSetupApi';
import type { BuildingNames } from '../../shared/api/buildingNames';
import { ActiveTag } from '../../shared/components/ActiveTag';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { focusRing } from '../../shared/components/styles';
import { areaMeta } from './areaText';

interface AreaListProps {
  areas: AreaSummary[] | undefined;
  total: number | undefined;
  selectedId: number | undefined;
  loading: boolean;
  names: BuildingNames;
  buildings: { id: number; code: string; name?: string }[];
  search: string;
  onSearch: (value: string) => void;
  buildingId: string;
  onBuilding: (value: string) => void;
  onClear: () => void;
  hasFilters: boolean;
}

const itemClass = `flex items-start justify-between gap-2 rounded-md border px-3 py-2 text-text no-underline ${focusRing}`;
const itemChosenClass = 'border-accent bg-accent-subtle';
const itemIdleClass = 'border-transparent hover:border-border-control';

/** All Areas remain available, including those in inactive buildings. Filters never change the route. */
export function AreaList({ areas, total, selectedId, loading, names, buildings, search, onSearch,
  buildingId, onBuilding, onClear, hasFilters }: AreaListProps) {
  return (
    <section className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-border bg-surface"
      aria-labelledby="areas-list-title">
      <h2 id="areas-list-title" className="sr-only">{`พื้นที่ทั้งหมด (${total ?? 0})`}</h2>
      <div className="flex flex-col gap-2 border-b border-border p-3">
        <Field label={<span className="inline-flex items-center gap-2"><Search aria-hidden="true" className="size-4" />ค้นหาพื้นที่</span>}>
          <TextInput size="desk" type="search" placeholder="ค้นหารหัสหรือชื่อพื้นที่" value={search}
            onChange={(event) => onSearch(event.target.value)} />
        </Field>
        <Field label="ตึก">
          <Select size="desk" value={buildingId} onChange={(event) => onBuilding(event.target.value)}>
            <option value="">ทุกตึก</option>
            {buildings.map((building) => <option key={building.id} value={building.id}>
              {building.name ? `${building.code} · ${building.name}` : building.code}
            </option>)}
          </Select>
        </Field>
      </div>
      {loading && <p className="px-3 py-3 text-text-2">กำลังโหลดพื้นที่...</p>}
      <ul className="m-0 flex max-h-116 list-none flex-col gap-0.5 overflow-y-auto p-1">
        {areas?.map((area) => (
          <li key={area.id}>
            <Link to={`/admin/areas/${area.id}`}
              className={`${itemClass} ${area.id === selectedId ? itemChosenClass : itemIdleClass}`}
              aria-current={area.id === selectedId ? 'true' : undefined}>
              <span className="flex min-w-0 flex-col" title={areaMeta(area, names)}>
                <span className="font-semibold">{`${area.code} ${area.name}`}</span>
                <span className="text-xs text-text-2">{`ตึก ${area.buildingCode} · ${area.activePointCount} จุด`}</span>
              </span>
              {!area.isActive && <ActiveTag active={false} />}
            </Link>
          </li>
        ))}
      </ul>
      {areas?.length === 0 && hasFilters && <p className="px-3 py-3 text-text-2">ไม่พบพื้นที่ที่ตรงกับตัวกรอง</p>}
      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border px-3 py-2 text-xs text-text-2">
        <span role="status">{total === undefined ? 'กำลังโหลดจำนวนพื้นที่...' : `แสดง ${areas?.length ?? 0} จาก ${total} พื้นที่`}</span>
        {hasFilters && <Button variant="link" size="desk" onClick={onClear}>ล้างตัวกรอง</Button>}
      </div>
    </section>
  );
}
