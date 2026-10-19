import { Link, useParams } from 'react-router';
import { useGetAreaQuery, useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaActiveSection } from './AreaActiveSection';
import { AreaForm } from './AreaForm';
import './areaForm.css';

/** facility-0045, wireframe D10: "+ เพิ่ม Area" opens the form empty, "แก้ไข Area" opens it filled. */
export function AreaFormPage() {
  const { areaId } = useParams();
  const id = areaId === undefined ? undefined : Number(areaId);
  const buildings = useGetBuildingsQuery();
  const cleaners = useGetCleanersQuery();
  const area = useGetAreaQuery(id ?? 0, { skip: id === undefined });
  const detail = id === undefined ? undefined : area.currentData;
  const error = buildings.error ?? cleaners.error ?? area.error;
  const title =
    id === undefined ? 'เพิ่ม Area' : detail ? `แก้ไข Area · ${detail.area.code} ${detail.area.name}` : 'แก้ไข Area';

  return (
    <main className="areaform-page">
      <Link to={id === undefined ? '/admin/areas' : `/admin/areas/${id}`}>‹ จัดการ Area และป้าย</Link>
      <h1>{title}</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {buildings.data && cleaners.data && (id === undefined || detail) ? (
        <AreaForm key={id ?? 'new'} detail={detail} buildings={buildings.data} cleaners={cleaners.data} />
      ) : (
        !error && <p className="app-muted">กำลังโหลด...</p>
      )}
      {detail && <AreaActiveSection detail={detail} />}
      <p className="app-muted">การบันทึกทุกครั้งเก็บลงประวัติ Admin</p>
    </main>
  );
}
