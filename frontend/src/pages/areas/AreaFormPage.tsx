import { ChevronLeft } from 'lucide-react';
import { Link, useParams } from 'react-router';
import { useGetAreaQuery, useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { isNotFound } from '../../shared/api/scanAnswers';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { linkClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaActiveSection } from './AreaActiveSection';
import { AreaForm } from './AreaForm';

/**
 * facility-0045, wireframe D10: "+ เพิ่ม Area" opens the form empty, "แก้ไข Area" opens it filled. The form keeps its
 * one column at 880 px, as the design file draws it.
 */
export function AreaFormPage() {
  const { areaId } = useParams();
  const id = areaId === undefined ? undefined : Number(areaId);
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const cleaners = useGetCleanersQuery(undefined, freshOnOpen);
  const area = useGetAreaQuery(id ?? 0, { ...freshOnOpen, skip: id === undefined });
  const detail = id === undefined ? undefined : area.currentData;
  const error = buildings.error ?? cleaners.error ?? area.error;
  const retryError = () => {
    if (buildings.error) void buildings.refetch();
    if (cleaners.error) void cleaners.refetch();
    if (area.error && !isNotFound(area.error)) void area.refetch();
  };
  const title =
    id === undefined ? 'เพิ่มพื้นที่' : detail ? `แก้ไขพื้นที่ · ${detail.area.code} ${detail.area.name}` : 'แก้ไขพื้นที่';

  return (
    // D10's one column at 880 px, so a name or a date is not a 1216 px line; no width token names it
    <main className="flex min-w-0 max-w-220 flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        back={
          <Link className={`${linkClass} inline-flex min-h-9 items-center gap-1 self-start font-semibold`} to={id === undefined ? '/admin/areas' : `/admin/areas/${id}`}>
            <ChevronLeft aria-hidden="true" className="size-4" />
            พื้นที่และป้าย
          </Link>
        }
        title={title}
      />
      {error && <Note tone="error" onRetry={isNotFound(error) ? undefined : retryError}>{getErrorMessage(error)}</Note>}
      {buildings.data && cleaners.data && (id === undefined || detail) ? (
        <AreaForm key={id ?? 'new'} detail={detail} buildings={buildings.data} cleaners={cleaners.data} />
      ) : (
        !error && <PageLoading>กำลังโหลด...</PageLoading>
      )}
      {detail && <AreaActiveSection detail={detail} />}
    </main>
  );
}
