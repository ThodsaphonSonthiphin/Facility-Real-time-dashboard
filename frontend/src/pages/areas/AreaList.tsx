import { Link } from 'react-router';
import type { AreaSummary } from '../../shared/api/adminSetupApi';
import { areaMeta } from './areaText';

interface AreaListProps {
  areas: AreaSummary[] | undefined;
  selectedId: number | undefined;
  loading: boolean;
}

/** Wireframe D4, left: every Area, deactivated ones too; the open one is marked. */
export function AreaList({ areas, selectedId, loading }: AreaListProps) {
  return (
    <section className="areas-list" aria-labelledby="areas-list-title">
      <div className="areas-list__head">
        <h2 id="areas-list-title">{`Area ทั้งหมด (${areas?.length ?? 0})`}</h2>
        <Link className="app-button" to="/admin/areas/new">
          + เพิ่ม Area
        </Link>
      </div>
      {loading && <p className="app-muted">กำลังโหลด Area...</p>}
      <ul>
        {areas?.map((area) => {
          const selected = area.id === selectedId;
          const className = [
            'areas-list__item',
            selected && 'areas-list__item--selected',
            !area.isActive && 'areas-list__item--inactive',
          ]
            .filter(Boolean)
            .join(' ');
          return (
            <li key={area.id}>
              <Link to={`/admin/areas/${area.id}`} className={className} aria-current={selected ? 'true' : undefined}>
                <strong>{`${area.code} ${area.name}`}</strong>
                <span>{areaMeta(area)}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
