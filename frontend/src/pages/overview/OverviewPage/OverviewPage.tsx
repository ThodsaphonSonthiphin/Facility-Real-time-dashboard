import { RotateCcw } from 'lucide-react';
import { Link } from 'react-router';
import { AdminPageHeader } from '../../../shared/components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../../shared/components/Button';
import { Field, Select, TextInput } from '../../../shared/components/Field';
import { LiveBadge } from '../../../shared/components/LiveBadge';
import { MoreInfo } from '../../../shared/components/MoreInfo';
import { Note } from '../../../shared/components/Note';
import { PageLoading } from '../../../shared/components/PageState';
import { Segmented } from '../../../shared/components/Segmented';
import { FilterToolbar } from '../../../shared/components/SurfacePanel';
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
    <main className="motion-view flex min-w-0 flex-col gap-5 text-sm text-text sm:gap-6">
      <AdminPageHeader
        title="ภาพรวมการดูแลพื้นที่"
        titleSize="prominent"
        subtitle={model.periodText}
        actions={model.hubKey ? <LiveBadge hubKey={model.hubKey} /> : undefined}
      />

      <p role="status" aria-label="สถานะภาพรวม" aria-live="polite" className="sr-only">
        {model.statusAnnouncement}
      </p>

      {model.invalidContext ? (
        <Note tone="error" title="ลิงก์หน้านี้ไม่ถูกต้อง">
          <p>{model.error}</p>
          <p className="mt-2">
            <Link className={linkClass} to="/admin/overview">
              กลับไปหน้าภาพรวมเริ่มต้น
            </Link>
          </p>
        </Note>
      ) : (
        <>
          <FilterToolbar
            label="ตัวกรองภาพรวม"
            align="start"
            appearance="plain"
            actions={
              model.canResetToCurrent ? (
                <Button size="desk" variant="secondary" onClick={model.onResetToCurrent}>
                  <RotateCcw aria-hidden="true" className="size-4" />
                  กลับกะปัจจุบัน
                </Button>
              ) : undefined
            }
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
                className="w-full max-w-full sm:w-auto sm:max-w-72"
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
          </FilterToolbar>

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

          {model.response && model.recovering && !model.error && (
            <Note tone="warn" status>
              {`กำลังตรวจสอบข้อมูลล่าสุด · กำลังแสดงข้อมูลที่อ่านเมื่อ ${model.readAtText} น. (ข้อมูลอาจยังไม่ล่าสุด)`}
            </Note>
          )}

          {model.refreshing && !model.recovering && !model.error && (
            <Note tone="info">
              {`กำลังอัปเดตข้อมูล... (กำลังแสดงข้อมูลที่อ่านเมื่อ ${model.readAtText} น.)`}
            </Note>
          )}

          {!model.refreshing && !model.recovering && !model.error && model.updatedNotice && (
            <Note tone="info">{model.updatedNotice}</Note>
          )}

          {model.notStartedMessage && (
            <Note tone="info" alert={false} title="กะนี้ยังไม่เริ่ม">
              {model.notStartedMessage}
            </Note>
          )}

          {model.response && (
            <>
              <section aria-labelledby="overview-shift-summary" className="rounded-lg border border-border bg-surface">
                <h2 id="overview-shift-summary" className="px-4 pt-4 text-lg font-bold">สรุปผลของกะ</h2>
                <div className="grid min-w-0 grid-cols-1 divide-y divide-border lg:grid-cols-3 lg:divide-x lg:divide-y-0">
                  {model.panels.map((panel) => (
                    <CoveragePanel key={panel.title} {...panel} onGroup={model.onGroup} />
                  ))}
                </div>
              </section>

              <div className="grid min-w-0 gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)] xl:gap-6">
                <FollowUpGroups {...model.followUp} onGroup={model.onGroup} />
                <BuildingComparison {...model.buildings} onSelect={model.onBuildingGroup} />
              </div>
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
