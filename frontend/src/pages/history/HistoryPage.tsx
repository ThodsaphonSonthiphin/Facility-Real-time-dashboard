import { Eye } from 'lucide-react';
import { Button } from '../../shared/components/Button';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { HistoryControls } from './HistoryControls';
import { HistoryTable } from './HistoryTable';
import { useHistoryPage } from './useHistoryPage';

/** Read-only audit history, with one visible cursor page and URL-backed filters. */
export function HistoryPage() {
  const view = useHistoryPage();
  return (
    <main className="flex min-w-0 flex-col gap-6 text-sm text-text">
      <header className="flex flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-2xl font-bold">ประวัติการแก้ไขของแอดมิน</h1>
          <span className="inline-flex items-center gap-1 rounded-sm border border-border bg-idle-bg px-2 py-0.5 text-xs font-semibold text-idle">
            <Eye aria-hidden="true" className="size-4" />อ่านอย่างเดียว
          </span>
        </div>
        <span className="min-h-5 text-text-2">ใคร แก้อะไร เมื่อไร ค่าก่อนและหลัง แก้หรือลบรายการไม่ได้</span>
      </header>
      <HistoryControls args={view.args} actors={view.actors.data} texts={view.texts} onTexts={view.setTexts}
        onShow={view.show} onArgs={view.go} search={view.search} onSearch={view.setSearch}
        preset={view.preset} onPreset={view.selectPreset} />
      {view.problem && <Note tone="error">{view.problem}</Note>}
      {view.log.isFetching && !view.log.currentData && <PageLoading>กำลังโหลด...</PageLoading>}
      {view.log.error && <Note tone="error">{getErrorMessage(view.log.error)}</Note>}
      {view.actors.error && <Note tone="error" onRetry={() => void view.actors.refetch()}>{getErrorMessage(view.actors.error)}</Note>}
      {view.log.currentData && (
        <HistoryTable entries={view.entries} emptyText={view.args.q ? 'ไม่พบรายการที่ตรงกับที่ค้นหาในช่วงนี้' : 'ไม่มีประวัติในช่วงนี้'}
          footer={
            <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-2.5">
              <span className="text-text-2">{view.rangeLabel}{view.args.q ? ` ที่ตรงกับ "${view.args.q}"` : ''}</span>
              <div className="flex gap-2">
                <Button size="desk" disabled={!view.hasPrevious || view.log.isFetching} onClick={view.previousPage}>ก่อนหน้า</Button>
                <Button size="desk" disabled={!view.hasNext || view.log.isFetching} onClick={() => void view.nextPage()}>
                  {view.log.isFetchingNextPage ? 'กำลังโหลด...' : 'ถัดไป'}
                </Button>
              </div>
            </div>
          } />
      )}
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>ไม่มีใครแก้หรือลบรายการในหน้านี้ได้ · เก็บนานเท่ากับข้อมูลที่ถูกแก้ · เบอร์โทรไม่แสดงทั้งก่อนและหลัง · การเปิดดูหน้านี้ไม่ถูกบันทึก</span>
      </MoreInfo>
    </main>
  );
}
