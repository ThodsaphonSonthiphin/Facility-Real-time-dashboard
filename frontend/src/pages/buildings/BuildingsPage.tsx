import { useState } from 'react';
import { useGetAllBuildingsQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Button } from '../../shared/components/Button';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { BuildingDrawer } from './BuildingDrawer';
import { BuildingTable } from './BuildingTable';

type Open = { kind: 'create' } | { kind: 'edit'; id: number } | null;

/**
 * facility-0090: every building, off ones too, in one table; a building is added and edited in the right-hand Drawer,
 * as D4's Area is. A save closes the Drawer and floats its answer. A building is never deleted, only turned off.
 */
export function BuildingsPage() {
  const buildings = useGetAllBuildingsQuery(undefined, freshOnOpen);
  const [open, setOpen] = useState<Open>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const list = buildings.data ?? [];
  const editing = open?.kind === 'edit' ? list.find((building) => building.id === open.id) : undefined;
  const close = () => setOpen(null);
  const done = (text: string) => {
    setOpen(null);
    setNotice({ kind: 'ok', text });
  };

  return (
    <main className="flex flex-col gap-4 text-sm text-text">
      <h1 className="text-xl font-bold">ตึก</h1>
      {buildings.error && <Note tone="error">{getErrorMessage(buildings.error)}</Note>}
      <section className="flex min-w-0 flex-col gap-2.5" aria-labelledby="buildings-list-title">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="buildings-list-title" className="text-base font-bold">{`ตึกทั้งหมด (${list.length})`}</h2>
          <Button size="desk" onClick={() => setOpen({ kind: 'create' })}>
            + เพิ่มตึก
          </Button>
        </div>
        {buildings.isLoading && <p className="text-text-2">กำลังโหลดตึก...</p>}
        {buildings.data?.length === 0 && <Note tone="info">ยังไม่มีตึก กด "+ เพิ่มตึก" เพื่อเริ่ม</Note>}
        {list.length > 0 && <BuildingTable buildings={list} onEdit={(building) => setOpen({ kind: 'edit', id: building.id })} />}
      </section>
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>
          ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่ · ปิดตึกได้เมื่อไม่มีพื้นที่ที่เปิดอยู่และไม่มีหัวหน้าที่เปิดใช้งานประจำตึก ·
          ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </span>
      </MoreInfo>
      {open?.kind === 'create' && <BuildingDrawer onClose={close} onDone={done} />}
      {editing && <BuildingDrawer key={editing.id} building={editing} onClose={close} onDone={done} />}
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
    </main>
  );
}
