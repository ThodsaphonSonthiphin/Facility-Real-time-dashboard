import { Download, FileSpreadsheet, ShieldAlert } from 'lucide-react';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { focusRing } from '../../shared/components/styles';
import { ExportBuildings } from './ExportBuildings';
import { exportSets, setDetail, setSummary, setTitle } from './exportRules';
import { useExportPage } from './useExportPage';

export function ExportPage() {
  const page = useExportPage();
  return (
    <main className="flex min-w-0 flex-col gap-6 text-sm text-text">
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">ส่งออก Excel</h1>
        <span className="text-text-2">เลือกชุดข้อมูล ช่วงวัน และตึก แล้วดาวน์โหลดเป็นไฟล์ .xlsx</span>
      </header>
      <div className="grid min-w-0 grid-cols-1 items-start gap-6 lg:grid-cols-[minmax(0,1fr)_280px]">
        <div className="min-w-0 overflow-hidden rounded-lg border border-border bg-surface">
          <section className={sectionClass} aria-labelledby="export-sets">
            <h2 id="export-sets" className={titleClass}>1. ชุดข้อมูล</h2>
            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
              {exportSets.map((set) => (
                <label
                  key={set}
                  className="flex cursor-pointer items-start gap-3 rounded-md border border-border-control px-4 py-3 has-checked:border-2 has-checked:border-accent has-checked:bg-accent-subtle has-checked:px-[15px] has-checked:py-[11px] has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-accent"
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
            <div className="flex flex-wrap gap-4">
              <Field label="ตั้งแต่">
                <TextInput
                  size="desk"
                  className="w-35"
                  value={page.texts.fromText}
                  placeholder="วว/ดด/ปปปป"
                  inputMode="numeric"
                  onChange={(event) => page.setTexts({ ...page.texts, fromText: event.target.value })}
                />
              </Field>
              <Field label="ถึง">
                <TextInput
                  size="desk"
                  className="w-35"
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
          <section className="flex flex-col gap-4 p-6" aria-label="ดาวน์โหลดไฟล์">
            <div className="flex items-start gap-3 rounded-md border border-overdue-border bg-overdue-bg p-4">
              <ShieldAlert aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-overdue" />
              <p>
                <strong>ไฟล์นี้มีข้อมูลส่วนบุคคล</strong><br />
                มีชื่อ รหัสพนักงาน และเวลาของแม่บ้าน ใช้เฉพาะงานที่เกี่ยวข้อง เก็บในที่ที่มีการควบคุมสิทธิ์
                และลบเมื่อไม่ใช้ การส่งออกทุกครั้งถูกบันทึกในประวัติการแก้ไข
              </p>
            </div>
            <label className="flex min-h-9 cursor-pointer items-center gap-2">
              <input
                type="checkbox"
                className={`mt-0.5 size-4.5 shrink-0 accent-accent ${focusRing}`}
                checked={page.acknowledged}
                onChange={(event) => page.setAcknowledged(event.target.checked)}
              />
              <span>เข้าใจแล้ว และจะเก็บไฟล์ตามข้อกำหนดข้อมูลส่วนบุคคล</span>
            </label>
            <Button
              className="self-end"
              variant="primary"
              size="desk"
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
        </div>
        <aside
          className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-6"
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
            <span>ไม่มีพิกัดตำแหน่งดิบ มีแค่ระยะห่างจากป้าย และผลในรัศมี / นอกรัศมี</span>
            <span>ไฟล์ที่ดาวน์โหลดแล้วห้ามส่งต่อนอกบริษัท และอยู่นอกการลบตามระยะเวลาเก็บข้อมูล</span>
          </MoreInfo>
        </aside>
      </div>
    </main>
  );
}

const sectionClass = 'flex flex-col gap-4 border-b border-border p-6';
const titleClass = 'text-base font-bold';
