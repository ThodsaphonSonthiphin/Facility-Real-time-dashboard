import { ChevronLeft } from 'lucide-react';
import { Link } from 'react-router';
import { AdminPageHeader } from '../../components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../components/Button';
import { LiveBadge } from '../../components/LiveBadge';
import { Note } from '../../components/Note';
import { PageLoading } from '../../components/PageState';
import { linkClass } from '../../components/styles';
import { AreaAttendance } from '../AreaAttendance/AreaAttendance';
import { AreaWork } from '../AreaWork/AreaWork';
import type { OverviewAreaPageProps } from './type';
import { useOverviewAreaPage } from './useOverviewAreaPage';

export function OverviewAreaPage({ view, actions }: OverviewAreaPageProps) {
  const model = useOverviewAreaPage(view);
  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        back={
          <Link className={`${linkClass} inline-flex min-h-9 items-center gap-1 self-start font-semibold`} to={model.backHref}>
            <ChevronLeft aria-hidden="true" className="size-4" />
            กลับภาพรวม
          </Link>
        }
        title={model.detail ? `${model.detail.area.code} ${model.detail.area.name}` : 'รายละเอียดพื้นที่'}
        subtitle={
          (model.periodText || model.phaseText) ? (
            <>
              {model.periodText && (
                <span className="block">
                  {model.periodText}
                  {model.detail && ` · ตึก ${model.detail.area.buildingName}`}
                </span>
              )}
              {model.phaseText && <span className="block">{model.phaseText}</span>}
            </>
          ) : undefined
        }
        actions={model.hubKey ? <LiveBadge hubKey={model.hubKey} /> : undefined}
      />
      {model.loading && <PageLoading lines={3}>กำลังโหลดพื้นที่...</PageLoading>}
      {model.error && (
        <Note tone={model.detail ? 'warn' : 'error'} onRetry={model.canRetry ? model.retry : undefined}>
          {model.detail
            ? `ไม่สามารถอัปเดตข้อมูลล่าสุดได้ (${model.error}) · กำลังแสดงข้อมูลที่อ่านเมื่อ ${model.readAtText} น. (ข้อมูลอาจยังไม่ล่าสุด)`
            : model.error}
        </Note>
      )}
      {model.detail && model.recovering && !model.error && (
        <Note tone="warn" status>
          {`กำลังตรวจสอบข้อมูลล่าสุด · กำลังแสดงข้อมูลที่อ่านเมื่อ ${model.readAtText} น. (ข้อมูลอาจยังไม่ล่าสุด)`}
        </Note>
      )}
      {model.detail && model.selection && (
        <>
          {model.stateChanged && <Note tone="info" status>สถานะพื้นที่เปลี่ยนจากรายการที่เลือกแล้ว</Note>}
          {model.readOnly && <Note tone="info">ประวัติสำหรับอ่านข้อมูล</Note>}
          {model.view === 'attendance'
            ? <AreaAttendance detail={model.detail} />
            : <AreaWork detail={model.detail} selection={model.selection} view={model.view} />}
          {model.partial && !model.error && (
            <Button className="self-start" size="desk" onClick={model.retry}>ลองโหลดข้อมูลอีกครั้ง</Button>
          )}
          {model.view === 'attendance' && !model.readOnly && !model.error && actions}
        </>
      )}
    </main>
  );
}
