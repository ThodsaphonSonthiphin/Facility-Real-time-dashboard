import { Plus } from 'lucide-react';
import { Button } from '../../shared/components/Button';
import { MoreInfo } from '../../shared/components/MoreInfo';
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
    <main className="flex flex-col gap-6 text-sm text-text">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">ตึก</h1>
          {/* As tall as its words before the answer, so nothing under it moves */}
          <span className="min-h-5 text-text-2">ตึกที่ปิดใช้งาน จะไม่แสดงในหน้าสถานะจุด และสแกนป้ายในตึกนั้นไม่ได้</span>
        </div>
        <Button variant="primary" size="desk" onClick={create}>
          <Plus aria-hidden="true" className="size-4" />
          เพิ่มตึก
        </Button>
      </header>
      {accounts.error && <Note tone="error" onRetry={() => void accounts.refetch()}>โหลดรายชื่อหัวหน้าไม่ได้</Note>}
      {buildings.error && <Note tone="error">{getErrorMessage(buildings.error)}</Note>}
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
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>
          ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่ · ปิดตึกได้เมื่อไม่มีพื้นที่ที่เปิดอยู่และไม่มีหัวหน้าที่เปิดใช้งานประจำตึก ·
          ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </span>
      </MoreInfo>
      {open?.kind === 'create' && <BuildingDrawer onClose={close} onDone={done} />}
      {editing && <BuildingDrawer key={editing.id} building={editing} onClose={close} onDone={done} />}
      {notice && <Toast notice={notice} onClose={dismissNotice} />}
    </main>
  );
}
