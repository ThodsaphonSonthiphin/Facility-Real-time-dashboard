import { ChevronDown, ChevronUp } from 'lucide-react';
import { Fragment } from 'react';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { PageEmpty } from '../../shared/components/PageState';
import { tableCell } from '../../shared/components/styles';
import { shiftShortLabel } from '../../shared/utils/labels';
import { SummaryRowDetails } from './SummaryRowDetails';
import { useSummaryTable } from './useSummaryTable';
import type { SummaryTableProps } from './type';

export function SummaryTable(props: SummaryTableProps) {
  const table = useSummaryTable(props);
  if (table.rows.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-surface">
        <PageEmpty title={props.filteredShift ? 'ไม่มีหัวหน้าที่เปิดใช้งานในกะที่เลือก' : 'ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก'}>
          เลือกตึกอื่นหรือทั้งสองกะเพื่อดูทุกคน
        </PageEmpty>
      </div>
    );
  }
  return (
    <Table label="การตรวจของหัวหน้าแต่ละคน" minWidth="min-w-230">
      <thead>
        <tr>
          <th scope="col">หัวหน้า</th>
          <th scope="col">ตึก · กะ</th>
          <th scope="col" className={tableCell.num}>ตรวจ (ครั้ง)</th>
          <th scope="col" className={tableCell.num}>ส่งแล้วไม่ได้ตรวจ</th>
          <th scope="col" className={tableCell.num}>ให้แก้</th>
          <th scope="col" className={tableCell.num}>ตรวจไกลป้าย</th>
          <th scope="col" className={tableCell.num}>จุดที่ไม่ได้ตรวจเลย</th>
          <th scope="col" className="relative"><span className="sr-only">รายละเอียด</span></th>
        </tr>
      </thead>
      <tbody>
        {table.rows.map(({ row, place, missed, id, expanded, share }) => (
          <Fragment key={row.supervisorId}>
            <tr>
              <th scope="row" className="whitespace-nowrap font-semibold">{row.displayName}</th>
              <td title={place} className="whitespace-nowrap">{row.buildingCode} · {shiftShortLabel[row.shift]}</td>
              <td className={tableCell.num}>{row.inspections}</td>
              <td className={`${tableCell.num} ${row.notInspected > 0 ? 'font-bold text-overdue' : 'text-text-2'}`}>{row.notInspected}</td>
              <td className={tableCell.num}>{row.reworks}</td>
              <td className={`${tableCell.num} ${row.gpsFlagged > 0 ? 'font-bold text-overdue' : 'text-text-2'}`}>{row.gpsFlagged}</td>
              <td className={`${tableCell.num} ${missed > 0 ? 'font-bold text-rework' : 'text-text-2'}`}>{missed}</td>
              <td className={tableCell.actions}>
                <Button variant="link" size="desk" aria-expanded={expanded} aria-controls={id}
                  aria-label={`${expanded ? 'ซ่อน' : missed > 0 ? 'ดูจุด' : 'รายละเอียด'} ${row.displayName}`}
                  onClick={() => table.toggle(row.supervisorId)}>
                  {expanded ? 'ซ่อน' : missed > 0 ? 'ดูจุด' : 'รายละเอียด'}
                  {expanded ? <ChevronUp aria-hidden="true" className="size-4" /> : <ChevronDown aria-hidden="true" className="size-4" />}
                </Button>
              </td>
            </tr>
            <SummaryRowDetails row={row} id={id} expanded={expanded} place={place} share={share} />
          </Fragment>
        ))}
      </tbody>
    </Table>
  );
}
