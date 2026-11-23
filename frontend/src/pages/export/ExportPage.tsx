import { Download, FileSpreadsheet, ShieldAlert } from 'lucide-react';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { SurfacePanel } from '../../shared/components/SurfacePanel';
import { focusRing, toggleClass } from '../../shared/components/styles';
import type { RangePreset } from '../../shared/utils/dateRange';
import { ExportBuildings } from './ExportBuildings';
import { exportSets, setDetail, setSummary, setTitle } from './exportRules';
import { useExportPage } from './useExportPage';

const exportPresets: { value: RangePreset; label: string }[] = [
  { value: 'today', label: 'วันนี้' },
  { value: 'week', label: '7 วันล่าสุด' },
  { value: 'month', label: 'เดือนนี้' },
];

export function ExportPage() {
  const page = useExportPage();
  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <AdminPageHeader title="ส่งออก Excel" />
      {/* Two-column layout begins at xl (1280px) so iPad portrait/landscape keeps full width for the export form; no theme width token matches 280px. */}
      <div className="grid min-w-0 grid-cols-1 items-start gap-4 xl:grid-cols-[minmax(0,1fr)_280px]">
        <SurfacePanel className="min-w-0">
          <section className={sectionClass} aria-labelledby="export-sets">
            <h2 id="export-sets" className={titleClass}>1. ชุดข้อมูล</h2>
            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
              {/* No spacing token matches 15px/11px: offset the extra checked border pixel to keep card geometry stable. */}
              {exportSets.map((set) => (
                <label
                  key={set}
                  className="flex cursor-pointer items-start gap-3 rounded-md border border-border-control px-4 py-3 has-checked:border-2 has-checked:border-accent has-checked:bg-accent-subtle has-checked:px-3.75 has-checked:py-2.75 has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-accent"
                >
                  <input
                    type="checkbox"
                    className={`mt-0.5 size-4.5 shrink-0 accent-accent ${focusRing}`}
                    checked={page.sets.includes(set)}
                    onChange={() => page.toggleSet(set)}
                  />
                  <span className="flex min-w-0 flex-col">
                    <strong>{setTitle[set]}</strong>
                    <span className="text-xs text-text-2">{setSummary[set]}</span>
                  </span>
                </label>
              ))}
            </div>
          </section>
          <section className={sectionClass} aria-labelledby="export-dates">
            <h2 id="export-dates" className={titleClass}>2. ช่วงวัน</h2>
            <div className="flex flex-wrap gap-2" role="group" aria-label="เลือกช่วงวันด่วน">
              {exportPresets.map((preset) => (
                <button
                  key={preset.value}
                  type="button"
                  data-tap=""
                  aria-pressed={page.activePreset === preset.value}
                  className={toggleClass}
                  onClick={() => page.selectDatePreset(preset.value)}
                >
                  {preset.label}
                </button>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-3 sm:flex sm:flex-wrap sm:gap-4">
              <Field label="ตั้งแต่">
                <TextInput
                  size="adaptive"
                  className="w-full sm:w-35"
                  value={page.texts.fromText}
                  placeholder="วว/ดด/ปปปป"
                  inputMode="numeric"
                  onChange={(event) => page.setTexts({ ...page.texts, fromText: event.target.value })}
                />
              </Field>
              <Field label="ถึง">
                <TextInput
                  size="adaptive"
                  className="w-full sm:w-35"
                  value={page.texts.toText}
                  placeholder="วว/ดด/ปปปป"
                  inputMode="numeric"
                  onChange={(event) => page.setTexts({ ...page.texts, toText: event.target.value })}
                />
              </Field>
            </div>
            <p className="text-xs text-text-2">{page.days !== undefined && `${page.days} วัน `}(เลือกได้ 1–31 วัน)</p>
          </section>
          <section className={sectionClass} aria-labelledby="export-buildings">
            <h2 id="export-buildings" className={titleClass}>3. ตึก</h2>
            {page.buildingLoading && <p role="status" className="text-text-2">กำลังโหลดตึก...</p>}
            {page.buildingError ? (
              <Note tone="error" onRetry={page.retryBuildings}>โหลดรายชื่อตึกไม่สำเร็จ</Note>
            ) : page.buildingsReady && (
              <ExportBuildings
                buildings={page.buildings}
                selection={page.buildingIds}
                selectedCount={page.selectedBuildings.length}
                onToggle={page.toggleBuilding}
                onToggleAll={page.toggleAllBuildings}
              />
            )}
            {page.missingBuilding && <Note tone="error">ไม่พบตึกที่เลือก กรุณาเลือกตึกใหม่</Note>}
          </section>
          <section className="flex flex-col gap-4 p-4 sm:p-6" aria-label="ดาวน์โหลดไฟล์">
            <div className="flex items-start gap-3 rounded-md border border-overdue-border bg-overdue-bg p-4">
              <ShieldAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-overdue" />
              <p>
                <strong>ไฟล์มีข้อมูลส่วนบุคคล</strong><br />
                ใช้เฉพาะงานที่เกี่ยวข้องและลบไฟล์เมื่อเลิกใช้
              </p>
            </div>
            <label className="flex min-h-9 cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                className={`mt-0.5 size-4.5 shrink-0 accent-accent ${focusRing}`}
                checked={page.acknowledged}
                onChange={(event) => page.setAcknowledged(event.target.checked)}
              />
              <span>รับทราบข้อกำหนดข้อมูลส่วนบุคคล</span>
            </label>
            <Button
              className="w-full sm:w-auto sm:self-end"
              variant="primary"
              size="adaptive"
              disabled={page.sending || !page.acknowledged || !page.buildingsReady || page.selectedBuildings.length === 0 || page.missingBuilding}
              onClick={() => void page.download()}
            >
              <Download aria-hidden="true" className="size-4" />
              {page.sending ? 'กำลังสร้างไฟล์...' : 'ดาวน์โหลด Excel'}
            </Button>
            {page.notice && (
              <Note tone={page.notice.kind === 'ok' ? 'success' : 'error'} status={page.notice.kind === 'ok'}>
                {page.notice.text}
              </Note>
            )}
          </section>
        </SurfacePanel>
        <aside
          className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 sm:p-6 xl:sticky xl:top-5"
          aria-labelledby="export-summary"
        >
          <h2 id="export-summary" className="flex items-center gap-2 text-base font-bold">
            <FileSpreadsheet aria-hidden="true" className="size-5 text-text-2" />สรุปไฟล์
          </h2>
          <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-2 wrap-anywhere">
            <dt className="text-text-2">ชุดข้อมูล</dt><dd className="font-semibold">{page.setsSummary}</dd>
            <dt className="text-text-2">ช่วงวัน</dt><dd className="font-semibold">{page.rangeSummary}</dd>
            <dt className="text-text-2">ตึก</dt><dd className="font-semibold">{page.buildingsSummary}</dd>
          </dl>
          <p className="text-xs text-text-2">เลือกได้ 1–31 วันต่อไฟล์</p>
          <MoreInfo summary="รายละเอียดไฟล์">
            <span className="wrap-anywhere">{page.fileName}</span>
            <span>{page.sets.length} แผ่นงาน · {page.setsSummary}</span>
            {page.sets.map((set) => <span key={set}><strong>{setTitle[set]}: </strong>{setDetail[set]}</span>)}
          </MoreInfo>
        </aside>
      </div>
    </main>
  );
}

const sectionClass = 'flex flex-col gap-4 border-b border-border p-4 sm:p-6';
const titleClass = 'text-base font-bold';
