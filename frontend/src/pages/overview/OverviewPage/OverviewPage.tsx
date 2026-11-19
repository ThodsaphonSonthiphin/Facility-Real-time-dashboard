import { RotateCcw } from 'lucide-react';
import { Link } from 'react-router';
import { Button } from '../../../shared/components/Button';
import { Field, Select, TextInput } from '../../../shared/components/Field';
import { MoreInfo } from '../../../shared/components/MoreInfo';
import { Note } from '../../../shared/components/Note';
import { PageLoading } from '../../../shared/components/PageState';
import { Segmented } from '../../../shared/components/Segmented';
import { linkClass } from '../../../shared/components/styles';
import { AreaResults } from '../AreaResults/AreaResults';
import { BuildingComparison } from '../BuildingComparison/BuildingComparison';
import { CoveragePanel } from '../CoveragePanel/CoveragePanel';
import { FollowUpGroups } from '../FollowUpGroups/FollowUpGroups';
import { RoundHistory } from '../RoundHistory/RoundHistory';
import { useOverviewPage } from './useOverviewPage';

/** Admin combined overview page: one consistent summary/list projection with scoped drill-through. */
export function OverviewPage() {
  const model = useOverviewPage();

  return (
    <main className="flex min-w-0 flex-col gap-6 text-sm text-text">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">ภาพรวมการดูแลพื้นที่</h1>
          <p className="text-text-2">{model.periodText}</p>
        </div>
        {model.canResetToCurrent && (
          <Button size="desk" variant="secondary" onClick={model.onResetToCurrent}>
            <RotateCcw aria-hidden="true" className="size-4" />
            กลับกะปัจจุบัน
          </Button>
        )}
      </header>

      <p role="status" aria-live="polite" className="sr-only">
        {model.statusAnnouncement}
      </p>

      {model.invalidContext ? (
        <Note tone="error" title="พารามิเตอร์หน้าไม่ถูกต้อง">
          <p>{model.error}</p>
          <p className="mt-2">
            <Link className={linkClass} to="/admin/overview">
              กลับไปหน้าภาพรวมเริ่มต้น
            </Link>
          </p>
        </Note>
      ) : (
        <>
          <section
            aria-label="เลือกบริบทภาพรวม"
            className="flex flex-wrap items-end gap-4 rounded-lg border border-border bg-surface p-4"
          >
            <Field label="วันที่ของกะ" help={model.dateHelpText}>
              <TextInput
                size="desk"
                type="date"
                value={model.dateInputValue}
                max={model.dateMaxValue}
                disabled={model.controlsDisabled}
                onChange={(event) => model.onDate(event.target.value)}
              />
            </Field>

            <div className="flex flex-col gap-1">
              <span className="font-semibold">กะ</span>
              <Segmented
                label="กะ"
                options={model.shiftOptions}
                value={model.shiftValue}
                disabled={model.controlsDisabled}
                onChange={model.onShift}
              />
            </div>

            <Field label="ตึก">
              <Select
                size="desk"
                value={model.buildingValue}
                disabled={model.controlsDisabled}
                onChange={(event) =>
                  model.onBuilding(event.target.value === '' ? undefined : Number(event.target.value))
                }
              >
                <option value="">ทุกตึก</option>
                {model.buildingOptions.map((building) => (
                  <option key={building.id} value={building.id}>
                    {building.label}
                  </option>
                ))}
              </Select>
            </Field>
          </section>

          {model.loading && <PageLoading lines={3}>กำลังโหลดภาพรวม...</PageLoading>}

          {!model.response && model.error && (
            <Note tone="error" onRetry={model.retry}>
              {model.error}
            </Note>
          )}

          {model.response && model.error && (
            <Note tone="warn" onRetry={model.retry}>
              {`ไม่สามารถอัปเดตข้อมูลล่าสุดได้ (${model.error}) · กำลังแสดงข้อมูลที่อ่านเมื่อ ${model.readAtText} น. (ข้อมูลอาจยังไม่ล่าสุด)`}
            </Note>
          )}

          {model.refreshing && !model.error && (
            <Note tone="info">
              {`กำลังอัปเดตข้อมูล... (กำลังแสดงข้อมูลที่อ่านเมื่อ ${model.readAtText} น.)`}
            </Note>
          )}

          {!model.refreshing && !model.error && model.updatedNotice && (
            <Note tone="info">{model.updatedNotice}</Note>
          )}

          {model.notStartedMessage && (
            <Note tone="info" alert={false} title="กะนี้ยังไม่เริ่ม">
              {model.notStartedMessage}
            </Note>
          )}

          {model.response && (
            <>
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
                {model.panels.map((panel) => (
                  <CoveragePanel key={panel.title} {...panel} onGroup={model.onGroup} />
                ))}
              </div>

              <FollowUpGroups {...model.followUp} onGroup={model.onGroup} />

              <BuildingComparison {...model.buildings} onSelect={model.onBuildingGroup} />

              <RoundHistory {...model.history} onGroup={model.onGroup} />

              <AreaResults {...model.results} onPage={model.onPage} />

              <MoreInfo summary="วิธีอ่านภาพรวม">
                <span>
                  ทุกตัวเลขในหน้านี้คำนวณจากเป้าหมายโครงสร้าง ณ ช่วงเวลาที่เลือกและนับจำนวนพื้นที่ (Area) ไม่ซ้ำภายในแต่ละกลุ่ม
                </span>
                <span>
                  ฐานการส่งงานและฐานการตรวจนับจากพื้นที่ที่มีจุดถึงรอบในขณะนั้นเท่านั้น ส่วนเครื่องหมาย “—” แสดงเมื่อข้อมูลประวัติไม่ครบหรือยังไม่เริ่มกะ
                </span>
              </MoreInfo>
            </>
          )}
        </>
      )}
    </main>
  );
}
