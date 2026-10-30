import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { Button } from '../../shared/components/Button';
import { Select } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { RangeFields } from '../../shared/components/RangeFields';
import { focusRing } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { SummaryTable } from './SummaryTable';
import { useInspectionSummaryPage } from './useInspectionSummaryPage';

/** facility-0100: coverage information, never a score or ranking. */
export function InspectionSummaryPage() {
  const page = useInspectionSummaryPage();
  return (
    <main className="flex min-w-0 flex-col gap-6 text-sm text-text">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">สรุปการตรวจ</h1>
          <span className="text-text-2">การตรวจงานของหัวหน้าแต่ละคน ตามช่วงวันที่เลือก</span>
        </div>
        <div className="flex max-w-full flex-wrap items-center gap-3">
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
        </div>
      </header>
      <Note tone="info">
        <span>ตัวเลขใช้ดูว่าการตรวจครอบคลุมหรือไม่ <strong>ระบบไม่ให้คะแนนหรือจัดอันดับหัวหน้า</strong></span>
      </Note>
      {page.isFetching && !page.summary && <PageLoading lines={3} />}
      {page.error && <Note tone="error" onRetry={() => void page.refetch()}>{getErrorMessage(page.error)}</Note>}
      {page.summary && <SummaryTable key={page.tableKey} rows={page.rows} filteredShift={Boolean(page.shift)} />}
      <span className="text-xs text-text-2">
        "ส่งแล้วไม่ได้ตรวจ" = แม่บ้านส่งงานแล้ว แต่จบรอบโดยไม่มีการตรวจ · "ตรวจไกลป้าย" = ตรวจตอนพิกัดอยู่นอกรัศมีป้าย
      </span>
      <details open={page.controlsOpen} onToggle={(event) => page.setControlsOpen(event.currentTarget.open)}>
        <summary className={`inline-flex min-h-9 cursor-pointer items-center rounded-md text-accent ${focusRing}`}>
          ตัวกรองเพิ่มเติม · ตึกและกำหนดช่วงวัน
        </summary>
        <div className="flex flex-col gap-3 pt-2">
          <span className="text-xs text-text-2">{page.rangeLine}</span>
          <div className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-surface px-4 py-3">
            <RangeFields value={page.texts} onChange={page.setTexts} onEnter={page.show} />
            <Button size="desk" onClick={page.show}>แสดง</Button>
            <BuildingSelect value={page.buildingId} onChange={page.selectBuilding} />
          </div>
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
      <span>งานที่แม่บ้านส่งทับด้วยงานใหม่ในรอบเดียวกันก่อนที่หัวหน้าจะตรวจ ก็ไม่นับ เพราะหัวหน้าตรวจได้เฉพาะงานล่าสุดของรอบ</span>
      <span>ดูรายละเอียดรายครั้งได้จากไฟล์ Excel</span>
    </MoreInfo>
  );
}
