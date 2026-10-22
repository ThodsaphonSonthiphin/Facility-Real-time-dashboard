import { useParams } from 'react-router';
import { useGetAreaQuery, useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaDetailPanel } from './AreaDetailPanel';
import { AreaList } from './AreaList';
import './areas.css';

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
    <main className="areas-page">
      <h1>จัดการพื้นที่ จุด และพิกัดป้าย</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {areaId !== undefined && selectedId === undefined && (
        <p className="app-note app-note--error" role="alert">
          ไม่พบพื้นที่นี้
        </p>
      )}
      <div className="areas-layout">
        <AreaList areas={areas.data} selectedId={selectedId} loading={areas.isLoading} />
        <div className="areas-main">
          {areas.data?.length === 0 && (
            <p className="app-note app-note--info">ยังไม่มีพื้นที่ กด "+ เพิ่มพื้นที่" เพื่อเริ่ม</p>
          )}
          {detail.isFetching && !detail.currentData && <p className="app-muted">กำลังโหลดพื้นที่...</p>}
          {detail.currentData && <AreaDetailPanel key={detail.currentData.area.id} detail={detail.currentData} />}
        </div>
      </div>
      <p className="app-muted">
        แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ ·
        ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
      </p>
    </main>
  );
}
