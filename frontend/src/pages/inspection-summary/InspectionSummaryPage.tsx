import { ClipboardCheck, Clock, MapPin, RotateCcw } from 'lucide-react';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { Button } from '../../shared/components/Button';
import { Select } from '../../shared/components/Field';
import { KpiCard } from '../../shared/components/KpiCard/KpiCard';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { RangeFields } from '../../shared/components/RangeFields';
import { FilterToolbar } from '../../shared/components/SurfacePanel';
import { focusRing } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { SummaryTable } from './SummaryTable';
import { useInspectionSummaryPage } from './useInspectionSummaryPage';

/** facility-0100: coverage information, never a score or ranking. */
export function InspectionSummaryPage() {
  const page = useInspectionSummaryPage();
  const totals = page.rows.reduce(
    (acc, row) => ({
      inspections: acc.inspections + row.inspections,
      notInspected: acc.notInspected + row.notInspected,
      gpsFlagged: acc.gpsFlagged + row.gpsFlagged,
      reworks: acc.reworks + row.reworks,
    }),
    { inspections: 0, notInspected: 0, gpsFlagged: 0, reworks: 0 },
  );

  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <header className="flex flex-col gap-1">
        <h1 className="flex items-center gap-2 text-2xl font-extrabold tracking-tight">
          <ClipboardCheck aria-hidden="true" className="size-4 shrink-0 text-text-2" />
          สรุปการตรวจ
        </h1>
        <span className="text-text-2">การตรวจงานของหัวหน้าแต่ละคน ตามช่วงวันที่เลือก</span>
      </header>
      <FilterToolbar label="ตัวกรองการตรวจ">
        <label className="inline-flex max-w-full items-center gap-2">
          <span className="shrink-0">ช่วงวัน</span>
          <Select size="desk" className="min-w-0 max-w-full" value={page.preset} onChange={(event) => page.selectPreset(event.target.value)}>
            <option value="today">วันนี้</option>
            <option value="week">{page.weekLabel}</option>
            <option value="month">เดือนนี้</option>
            <option value="custom">{page.customLabel}</option>
          </Select>
        </label>
        <label className="inline-flex items-center gap-2">
          กะ
          <Select size="desk" value={page.shift} onChange={(event) => page.setShift(event.target.value as typeof page.shift)}>
            <option value="">ทั้งสองกะ</option>
            <option value="Day">กะเช้า</option>
            <option value="Night">กะดึก</option>
          </Select>
        </label>
      </FilterToolbar>
      <Note tone="info">
        <span>ตัวเลขใช้ดูว่าการตรวจครอบคลุมหรือไม่ <strong>ระบบไม่ให้คะแนนหรือจัดอันดับหัวหน้า</strong></span>
      </Note>
      {page.summary && (
        <section aria-label="สรุปตัวเลขการตรวจ" className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <KpiCard label="ตรวจแล้วทั้งหมด" value={totals.inspections} unit="ครั้ง" icon={ClipboardCheck} tone="blue" />
          <KpiCard label="ส่งแล้วไม่ได้ตรวจ" value={totals.notInspected} unit="รอบ" icon={Clock} tone="amber" />
          <KpiCard label="ตรวจไกลป้าย" value={totals.gpsFlagged} unit="ครั้ง" icon={MapPin} tone="amber" />
          <KpiCard label="ให้แก้ไข" value={totals.reworks} unit="ครั้ง" icon={RotateCcw} tone="red" />
        </section>
      )}
      {page.isFetching && !page.summary && <PageLoading lines={3} />}
      {page.error && <Note tone="error" onRetry={() => void page.refetch()}>{getErrorMessage(page.error)}</Note>}
      {page.summary && <SummaryTable key={page.tableKey} rows={page.rows} filteredShift={Boolean(page.shift)} />}
      <span className="text-xs text-text-2">
        “ส่งแล้วไม่ได้ตรวจ” คือรอบที่แม่บ้านส่งงานไว้และไม่มีผลตรวจ ส่วน “ตรวจไกลป้าย” คือผลตรวจที่ระบบแจ้งว่าตำแหน่งมือถืออาจอยู่ไกลจากป้ายหรืออ่านได้ไม่แม่น
      </span>
      <details open={page.controlsOpen} onToggle={(event) => page.setControlsOpen(event.currentTarget.open)}>
        <summary className={`inline-flex min-h-9 cursor-pointer items-center rounded-md text-accent ${focusRing}`}>
          ตัวกรองเพิ่มเติม · ตึกและกำหนดช่วงวัน
        </summary>
        <div className="flex flex-col gap-3 pt-2">
          <span className="text-xs text-text-2">{page.rangeLine}</span>
          <FilterToolbar label="ตัวกรองเพิ่มเติม" align="end">
            <RangeFields value={page.texts} onChange={page.setTexts} onEnter={page.show} />
            <Button size="desk" onClick={page.show}>แสดง</Button>
            <BuildingSelect value={page.buildingId} onChange={page.selectBuilding} />
          </FilterToolbar>
          {page.problem && <Note tone="error">{page.problem}</Note>}
        </div>
      </details>
      <SummaryNotes />
    </main>
  );
}

function SummaryNotes() {
  return (
    <MoreInfo summary="วิธีนับตัวเลข">
      <span>
        ไม่มีขั้นต่ำว่าหัวหน้าต้องตรวจกี่ครั้ง · "จุดที่ตรวจแล้ว" นับจุดในตึกและกะของหัวหน้าที่หัวหน้าคนนั้นตรวจอย่างน้อย 1 ครั้งในช่วงที่เลือก
      </span>
      <span>
        "ตรวจ (ครั้ง)" นับทุกครั้งที่หัวหน้าคนนั้นกดบันทึกผลตรวจ งานชิ้นเดียวตรวจซ้ำได้จึงนับหลายครั้ง และรวมตอนตรวจแทนตึกอื่น
      </span>
      <span>งานที่แม่บ้านส่งหลังจบกะไม่นับในตัวเลขของหัวหน้า เพราะไม่มีใครตรวจได้แล้ว</span>
      <span>ถ้าแม่บ้านส่งงานใหม่ก่อนหัวหน้าตรวจในรอบเดียวกัน ระบบนับเฉพาะงานที่ส่งล่าสุด</span>
      <span>ดูรายละเอียดรายครั้งได้จากไฟล์ Excel</span>
    </MoreInfo>
  );
}
