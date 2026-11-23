import { ChevronLeft, Printer } from 'lucide-react';
import { Link } from 'react-router';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../shared/components/Button';
import { Select } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../shared/components/PageState';
import { Segmented } from '../../shared/components/Segmented';
import { FilterToolbar } from '../../shared/components/SurfacePanel';
import { linkClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import type { SignSize } from './printRules';
import { SignCard } from './SignCard';
import { usePrintSignsPage } from './usePrintSignsPage';

/** D5 changes only the screen frame. The signs and A4 sheet geometry are unchanged. */
export function PrintSignsPage() {
  const {
    which, signId, size, setSize, areas, print, activeAreas, groups, sheets, summary,
    emptyMessage, disabled, printing, singleArea, printNow, selectArea,
  } = usePrintSignsPage();

  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text print:gap-0">
      <AdminPageHeader
        className="print:hidden"
        title="พิมพ์ป้าย"
        subtitle={summary}
        actions={
          <Button variant="primary" size="desk" disabled={disabled} onClick={() => void printNow()}>
            <Printer aria-hidden="true" className="size-4" />
            พิมพ์
          </Button>
        }
      />
      <FilterToolbar
        label="ตัวเลือก"
        className="min-w-0 print:hidden"
      >
        <label className="inline-flex min-w-0 max-w-full flex-wrap items-center gap-2">
          พื้นที่
          <Select
            size="desk"
            className="w-60 min-w-0 max-w-full"
            value={which}
            disabled={printing || areas.isLoading}
            onChange={(event) => selectArea(event.target.value)}
          >
            <option value="" disabled>เลือกพื้นที่</option>
            {activeAreas.map((area) => (
              <option key={area.id} value={String(area.id)}>{`${area.code} ${area.name} · ตึก ${area.buildingCode}`}</option>
            ))}
            {groups.map((group) => (
              <option key={group.id} value={`building:${group.id}`}>{`ทุกพื้นที่ในตึก ${group.code} (${group.count})`}</option>
            ))}
            <option value="all">{`ทุกพื้นที่ (${activeAreas.length})`}</option>
          </Select>
        </label>
        <div className="flex flex-wrap items-center gap-2">
          <span>ขนาดป้าย</span>
          <Segmented label="ขนาดป้าย" options={sizeOptions} value={size} onChange={setSize} disabled={printing} />
        </div>
      </FilterToolbar>
      {(areas.isLoading || areas.error || areas.data?.length === 0 || print.isFetching || print.error ||
        which === '' || emptyMessage || (signId !== undefined && singleArea)) && (
        <div className="flex min-w-0 flex-col gap-3 print:hidden">
          {areas.isLoading && <PageLoading>กำลังโหลดพื้นที่...</PageLoading>}
          {areas.error && <Note tone="error" onRetry={() => void areas.refetch()}>{getErrorMessage(areas.error)}</Note>}
          {areas.data?.length === 0 && <PageEmpty title="ยังไม่มีพื้นที่" />}
          {print.isFetching && <PageLoading>กำลังโหลดป้าย...</PageLoading>}
          {print.error && <Note tone="error" onRetry={() => void print.refetch()}>{getErrorMessage(print.error)}</Note>}
          {which === '' && <Note tone="info">เลือกพื้นที่ที่จะพิมพ์</Note>}
          {emptyMessage && <Note tone="warn">{emptyMessage}</Note>}
          {signId !== undefined && singleArea && (
            <p className="text-text-2">พิมพ์ป้ายเดียว{' '}
              <Link className={`${linkClass} inline-flex min-h-9 items-center`} to={`/admin/print?area=${which}`}>
                พิมพ์ทั้งพื้นที่
              </Link>
            </p>
          )}
        </div>
      )}
      {sheets.length > 0 && (
        <div className="min-w-0 max-w-full overflow-x-auto rounded-lg bg-skeleton p-6 print:contents">
          <div className="flex w-max min-w-full flex-col gap-6 print:contents">
            {sheets.map((sheet, index) => (
              <section
                key={index}
                className={`${sheetClass} ${sheetGrid[size]} ${index < sheets.length - 1 ? 'print:break-after-page' : ''}`}
                aria-label={`แผ่นที่ ${index + 1}`}
              >
                {sheet.map((sign) => <SignCard key={sign.key} sign={sign} size={size} />)}
              </section>
            ))}
          </div>
        </div>
      )}
      <div className="flex flex-col gap-1.5 print:hidden">
        <p className="text-text-2">{`ที่อยู่บนป้าย: ${window.location.origin}`}</p>
        {window.location.protocol !== 'https:' && (
          <Note tone="warn">เปิดจากลิงก์ HTTPS เพื่อให้มือถืออ่านตำแหน่งได้</Note>
        )}
        <Link className={`${linkClass} inline-flex min-h-9 items-center gap-1 self-start font-semibold`}
          to={singleArea ? `/admin/areas/${which}` : '/admin/areas'}>
          <ChevronLeft aria-hidden="true" className="size-4" />พื้นที่และป้าย
        </Link>
      </div>
    </main>
  );
}

const sizeOptions: { value: SignSize; label: string }[] = [
  { value: 'small', label: 'เล็ก (6 ต่อแผ่น)' },
  { value: 'A5', label: 'A5 (2 ต่อแผ่น)' },
];

// One A4 sheet: 210 × 297 mm with an 8 mm margin, signs cut along the dashed lines. The sheet is measured in
// millimetres as printed before the move (facility-0087 rule 8); no token is in mm. On screen it floats on the page
const sheetClass = 'mx-auto grid h-[297mm] w-[210mm] border border-border bg-sign-paper p-[8mm] print:m-0 print:border-0';

const sheetGrid: Record<SignSize, string> = {
  small: 'grid-cols-2 grid-rows-3',
  A5: 'grid-cols-1 grid-rows-2',
};
