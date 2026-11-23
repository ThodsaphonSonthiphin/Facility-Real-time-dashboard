import { Plus } from 'lucide-react';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../shared/components/PageState';
import { Toast } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { BuildingDrawer } from './BuildingDrawer';
import { BuildingTable } from './BuildingTable';
import { useBuildingsPage } from './useBuildingsPage';

/**
 * facility-0090: every building, off ones too, in one table; a building is added and edited in the right-hand Drawer,
 * as D4's Area is. A save closes the Drawer and floats its answer. A building is never deleted, only turned off.
 */
export function BuildingsPage() {
  const {
    buildings, accounts, list, supervisors, supervisorState, open, editing, notice,
    close, done, create, edit, dismissNotice,
  } = useBuildingsPage();

  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        title="ตึก"
        actions={
          <Button variant="primary" size="desk" onClick={create}>
            <Plus aria-hidden="true" className="size-4" />
            เพิ่มตึก
          </Button>
        }
      />
      {accounts.error && <Note tone="error" onRetry={() => void accounts.refetch()}>โหลดรายชื่อหัวหน้าไม่ได้</Note>}
      {buildings.error && <Note tone="error" onRetry={() => void buildings.refetch()}>{getErrorMessage(buildings.error)}</Note>}
      <section
        className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-border bg-surface"
        aria-labelledby="buildings-list-title"
      >
        <div className="sr-only">
          <h2 id="buildings-list-title" className="text-base font-bold">{`ตึกทั้งหมด (${list.length})`}</h2>
        </div>
        {buildings.isLoading && (
          <div className="px-4 py-3">
            <PageLoading>กำลังโหลดตึก...</PageLoading>
          </div>
        )}
        {buildings.data?.length === 0 && (
          <PageEmpty title="ยังไม่มีตึก">เริ่มจากปุ่ม "เพิ่มตึก" ด้านบน</PageEmpty>
        )}
        {list.length > 0 && (
          <BuildingTable buildings={list} supervisors={supervisors} supervisorState={supervisorState} onEdit={edit} />
        )}
      </section>
      {open?.kind === 'create' && <BuildingDrawer onClose={close} onDone={done} />}
      {editing && <BuildingDrawer key={editing.id} building={editing} onClose={close} onDone={done} />}
      {notice && <Toast notice={notice} onClose={dismissNotice} />}
    </main>
  );
}
