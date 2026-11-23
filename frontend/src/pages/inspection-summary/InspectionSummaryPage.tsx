import { ClipboardCheck, Clock, MapPin, RotateCcw } from 'lucide-react';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { Button } from '../../shared/components/Button';
import { Field, Select } from '../../shared/components/Field';
import { KpiCard } from '../../shared/components/KpiCard/KpiCard';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { RangeFields } from '../../shared/components/RangeFields';
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
      <AdminPageHeader title="สรุปการตรวจ" />
      <div className="flex flex-col gap-3">
        <section
          role="region"
          aria-label="ตัวกรองการตรวจ"
          className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4"
        >
          <div className="grid grid-cols-1 items-end gap-3 sm:grid-cols-3 lg:flex lg:flex-wrap">
            <Field label="ช่วงวัน">
              <Select size="adaptive" className="w-full min-w-0 max-w-full lg:w-auto" value={page.preset} onChange={(event) => page.selectPreset(event.target.value)}>
                <option value="today">วันนี้</option>
                <option value="week">{page.weekLabel}</option>
                <option value="month">เดือนนี้</option>
                <option value="custom">{page.customLabel}</option>
              </Select>
            </Field>
            <Field label="กะ">
              <Select size="adaptive" className="w-full lg:w-auto" value={page.shift} onChange={(event) => page.setShift(event.target.value as typeof page.shift)}>
                <option value="">ทั้งสองกะ</option>
                <option value="Day">กะเช้า</option>
                <option value="Night">กะดึก</option>
              </Select>
            </Field>
            <BuildingSelect size="adaptive" selectClassName="w-full max-w-full lg:w-auto lg:max-w-72" value={page.buildingId} onChange={page.selectBuilding} />
          </div>
          {page.preset === 'custom' && (
            <div className="flex w-full flex-wrap items-end gap-2 border-t border-border pt-3">
              <RangeFields value={page.texts} onChange={page.setTexts} onEnter={page.show} />
              <Button size="adaptive" className="w-full sm:w-auto" onClick={page.show}>แสดง</Button>
            </div>
          )}
        </section>
        {page.problem && <Note tone="error">{page.problem}</Note>}
      </div>
      {page.summary && (
        <section aria-label="สรุปตัวเลขการตรวจ" className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <KpiCard label="ตรวจแล้วทั้งหมด" value={totals.inspections} unit="ครั้ง" icon={ClipboardCheck} tone="neutral" />
          <KpiCard label="ส่งแล้วไม่ได้ตรวจ" value={totals.notInspected} unit="รอบ" icon={Clock} tone="amber" />
          <KpiCard label="ตรวจไกลป้าย" value={totals.gpsFlagged} unit="ครั้ง" icon={MapPin} tone="amber" />
          <KpiCard label="ให้แก้ไข" value={totals.reworks} unit="ครั้ง" icon={RotateCcw} tone="red" />
        </section>
      )}
      {page.isFetching && !page.summary && <PageLoading lines={3} />}
      {page.error && <Note tone="error" onRetry={() => void page.refetch()}>{getErrorMessage(page.error)}</Note>}
      {page.summary && <SummaryTable key={page.tableKey} rows={page.rows} filteredShift={Boolean(page.shift)} />}
    </main>
  );
}
