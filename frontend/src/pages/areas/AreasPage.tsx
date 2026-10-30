import { Plus } from 'lucide-react';
import { Link } from 'react-router';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../shared/components/PageState';
import { boxClass, buttonClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaDetailPanel } from './AreaDetailPanel';
import { AreaList } from './AreaList';
import { areaTotals } from './areaText';
import { useAreasPage } from './useAreasPage';

/**
 * facility-0045, wireframe D4: the Areas on the left, the open Area's signs on the right. Without an id in the
 * address, the first Area is open. `currentData` is the open Area's own answer, never the previous Area's.
 */
export function AreasPage() {
  const {
    areaId, areas, detail, selectedId, names, buildings, filteredAreas, search, setSearch,
    buildingId, setBuildingId, clearFilters, hasFilters, error,
  } = useAreasPage();

  return (
    <main className="flex flex-col gap-6 text-sm text-text">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">พื้นที่และป้าย</h1>
          {/* As tall as its words before the answer, so nothing under it moves */}
          <span className="min-h-5 text-text-2">{areas.data ? areaTotals(areas.data) : '\u00A0'}</span>
        </div>
        <Link className={buttonClass('primary', 'desk')} data-tap="" to="/admin/areas/new">
          <Plus aria-hidden="true" className="size-4" />
          เพิ่มพื้นที่
        </Link>
      </header>
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {areaId !== undefined && selectedId === undefined && <Note tone="error">ไม่พบพื้นที่นี้</Note>}
      {/* the design file's list rail, then the rest; no token describes a two-column track list */}
      <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-[280px_minmax(0,1fr)]">
        <AreaList
          areas={filteredAreas} total={areas.data?.length} selectedId={selectedId} loading={areas.isLoading}
          names={names} buildings={buildings} search={search} onSearch={setSearch}
          buildingId={buildingId} onBuilding={setBuildingId} onClear={clearFilters} hasFilters={hasFilters}
        />
        <div className="flex min-w-0 flex-col gap-4">
          {areas.data?.length === 0 && (
            <div className={boxClass}>
              <PageEmpty title="ยังไม่มีพื้นที่">เริ่มจากปุ่ม "เพิ่มพื้นที่" ด้านบน</PageEmpty>
            </div>
          )}
          {detail.isFetching && !detail.currentData && <PageLoading>กำลังโหลดพื้นที่...</PageLoading>}
          {detail.currentData && <AreaDetailPanel key={detail.currentData.area.id} detail={detail.currentData} />}
        </div>
      </div>
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>
          แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ ·
          ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </span>
      </MoreInfo>
    </main>
  );
}
