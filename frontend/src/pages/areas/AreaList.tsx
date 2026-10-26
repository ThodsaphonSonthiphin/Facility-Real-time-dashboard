import { Link } from 'react-router';
import type { AreaSummary } from '../../shared/api/adminSetupApi';
import { boxClass, buttonClass, focusRing } from '../../shared/components/styles';
import { areaMeta } from './areaText';

interface AreaListProps {
  areas: AreaSummary[] | undefined;
  selectedId: number | undefined;
  loading: boolean;
}

// A row of the list: the whole row is the link; the open Area carries a blue frame, since blue means "pressed/current"
const itemClass = `flex flex-col gap-1 rounded border px-2.5 py-2 text-text no-underline ${focusRing}`;
const itemChosenClass = 'border-primary bg-info-bg';
const itemIdleClass = 'border-transparent hover:border-input-border';

/** Wireframe D4, left: every Area, deactivated ones too; the open one is marked. */
export function AreaList({ areas, selectedId, loading }: AreaListProps) {
  return (
    <section className={`flex flex-col gap-2 ${boxClass}`} aria-labelledby="areas-list-title">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="areas-list-title" className="text-base font-bold">{`พื้นที่ทั้งหมด (${areas?.length ?? 0})`}</h2>
        <Link className={buttonClass('secondary', 'desk')} data-tap="" to="/admin/areas/new">
          + เพิ่มพื้นที่
        </Link>
      </div>
      {loading && <p className="text-muted">กำลังโหลดพื้นที่...</p>}
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {areas?.map((area) => {
          const selected = area.id === selectedId;
          return (
            <li key={area.id}>
              <Link
                to={`/admin/areas/${area.id}`}
                className={`${itemClass} ${selected ? itemChosenClass : itemIdleClass}`}
                aria-current={selected ? 'true' : undefined}
              >
                <span className={area.isActive ? 'font-semibold' : 'font-semibold text-muted'}>{`${area.code} ${area.name}`}</span>
                <span className="text-muted">{areaMeta(area)}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
