import { useParams } from 'react-router';
import { useGetAreaQuery, useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaDetailPanel } from './AreaDetailPanel';
import { AreaList } from './AreaList';

/**
 * facility-0045, wireframe D4: the Areas on the left, the open Area's signs on the right. Without an id in the
 * address, the first Area is open. `currentData` is the open Area's own answer, never the previous Area's.
 */
export function AreasPage() {
  const { areaId } = useParams();
  const areas = useGetAreasQuery(undefined, freshOnOpen);
  const parsed = areaId === undefined ? areas.data?.[0]?.id : Number(areaId);
  const selectedId = parsed !== undefined && Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
  const detail = useGetAreaQuery(selectedId ?? 0, { ...freshOnOpen, skip: selectedId === undefined });
  const error = areas.error ?? detail.error;

  return (
    <main className="flex flex-col gap-4 text-sm text-text">
      <h1 className="text-xl font-bold">จัดการพื้นที่ จุด และพิกัดป้าย</h1>
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {areaId !== undefined && selectedId === undefined && <Note tone="error">ไม่พบพื้นที่นี้</Note>}
      {/* the 300 px rail of the wireframe, then the rest; no token describes a two-column track list */}
      <div className="grid grid-cols-1 items-start gap-4 lg:grid-cols-[300px_minmax(0,1fr)]">
        <AreaList areas={areas.data} selectedId={selectedId} loading={areas.isLoading} />
        <div className="flex min-w-0 flex-col gap-3">
          {areas.data?.length === 0 && <Note tone="info">ยังไม่มีพื้นที่ กด "+ เพิ่มพื้นที่" เพื่อเริ่ม</Note>}
          {detail.isFetching && !detail.currentData && <p className="text-muted">กำลังโหลดพื้นที่...</p>}
          {detail.currentData && <AreaDetailPanel key={detail.currentData.area.id} detail={detail.currentData} />}
        </div>
      </div>
      <p className="text-muted">
        แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ ·
        ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
      </p>
    </main>
  );
}
