import { Link, useParams } from 'react-router';
import { useGetAreaQuery, useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Note } from '../../shared/components/Note';
import { linkClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaActiveSection } from './AreaActiveSection';
import { AreaForm } from './AreaForm';

/**
 * facility-0045, wireframe D10: "+ เพิ่ม Area" opens the form empty, "แก้ไข Area" opens it filled. The form keeps its
 * one column at 720 px, in the Admin look (pages/admin.md).
 */
export function AreaFormPage() {
  const { areaId } = useParams();
  const id = areaId === undefined ? undefined : Number(areaId);
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const cleaners = useGetCleanersQuery(undefined, freshOnOpen);
  const area = useGetAreaQuery(id ?? 0, { ...freshOnOpen, skip: id === undefined });
  const detail = id === undefined ? undefined : area.currentData;
  const error = buildings.error ?? cleaners.error ?? area.error;
  const title =
    id === undefined ? 'เพิ่มพื้นที่' : detail ? `แก้ไขพื้นที่ · ${detail.area.code} ${detail.area.name}` : 'แก้ไขพื้นที่';

  return (
    // D10's old one-column form: 720 px, so a name or a date is not a 1150 px line; no width token names it
    <main className="flex max-w-180 flex-col gap-4 text-sm text-text">
      <Link className={linkClass} to={id === undefined ? '/admin/areas' : `/admin/areas/${id}`}>
        ‹ จัดการพื้นที่และป้าย
      </Link>
      <h1 className="text-xl font-bold">{title}</h1>
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {buildings.data && cleaners.data && (id === undefined || detail) ? (
        <AreaForm key={id ?? 'new'} detail={detail} buildings={buildings.data} cleaners={cleaners.data} />
      ) : (
        !error && <p className="text-muted">กำลังโหลด...</p>
      )}
      {detail && <AreaActiveSection detail={detail} />}
      <p className="text-muted">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</p>
    </main>
  );
}
