import { ClipboardCheck, Clock, MapPin, RotateCcw } from 'lucide-react';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { Button } from '../../shared/components/Button';
import { Select } from '../../shared/components/Field';
import { KpiCard } from '../../shared/components/KpiCard/KpiCard';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { RangeFields } from '../../shared/components/RangeFields';
import { FilterToolbar } from '../../shared/components/SurfacePanel';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { SummaryTable } from './SummaryTable';
import { useInspectionSummaryPage } from './useInspectionSummaryPage';

/**
 * Inspection coverage summary (`/admin/inspection-summary`): keeps ALL filters (date preset, shift, building, and
 * custom date range) together at the top above the KPIs and table (Heuristics #4, #6, #8).
 */
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
      <AdminPageHeader
        title="สรุปการตรวจ"
        subtitle="ดูความครอบคลุมการตรวจของหัวหน้าแต่ละคนตามช่วงวันที่เลือก · ระบบไม่ให้คะแนนหรือจัดอันดับ"
      />
      <div className="flex flex-col gap-3">
        <FilterToolbar label="ตัวกรองการตรวจ" align="end">
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
          <BuildingSelect value={page.buildingId} onChange={page.selectBuilding} />
          {page.preset === 'custom' && (
            <div className="flex w-full flex-wrap items-end gap-2 border-t border-border pt-3">
              <RangeFields value={page.texts} onChange={page.setTexts} onEnter={page.show} />
              <Button size="desk" onClick={page.show}>แสดง</Button>
            </div>
          )}
        </FilterToolbar>
        {page.problem && <Note tone="error">{page.problem}</Note>}
      </div>
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
      <SummaryNotes />
    </main>
  );
}

function SummaryNotes() {
  return (
    <MoreInfo summary="วิธีนับตัวเลข">
      <span>
        ตัวเลขใช้ดูว่าการตรวจครอบคลุมหรือไม่ ระบบไม่ให้คะแนนหรือจัดอันดับหัวหน้า · ไม่มีขั้นต่ำว่าหัวหน้าต้องตรวจกี่ครั้ง
      </span>
      <span>
        &quot;จุดที่ตรวจแล้ว&quot; นับจุดในตึกและกะของหัวหน้าที่หัวหน้าคนนั้นตรวจอย่างน้อย 1 ครั้งในช่วงที่เลือก
      </span>
      <span>
        &quot;ตรวจ (ครั้ง)&quot; นับทุกครั้งที่หัวหน้าคนนั้นกดบันทึกผลตรวจ งานชิ้นเดียวตรวจซ้ำได้จึงนับหลายครั้ง และรวมตอนตรวจแทนตึกอื่น
      </span>
      <span>งานที่แม่บ้านส่งหลังจบกะไม่นับในตัวเลขของหัวหน้า เพราะไม่มีใครตรวจได้แล้ว</span>
      <span>ถ้าแม่บ้านส่งงานใหม่ก่อนหัวหน้าตรวจในรอบเดียวกัน ระบบนับเฉพาะงานที่ส่งล่าสุด · ดูรายละเอียดรายครั้งได้จากไฟล์ Excel</span>
    </MoreInfo>
  );
}
