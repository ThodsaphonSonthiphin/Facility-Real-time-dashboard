import { Pencil, Plus, QrCode } from 'lucide-react';
import { Link } from 'react-router';
import type { AreaDetail, AdminPoint } from '../../shared/api/adminSetupApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { PageEmpty } from '../../shared/components/PageState';
import { buttonClass } from '../../shared/components/styles';
import type { Shift } from '../../shared/api/types';
import { SignPlace } from './SignPlace';

/** Which point the Drawer holds the editor of. */
export type EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' };

/** One shift's windows in a table cell: "07:00–09:00 · 16:00–18:00", or a dash when the shift has none. */
function shiftRounds(point: AdminPoint, shift: Shift): string {
  const times = point.roundWindows
    .filter((window) => window.shift === shift)
    .map((window) => `${window.start}–${window.end}`);
  return times.length === 0 ? '—' : times.join(' · ');
}

/** Area points keep their place/shift columns; QR management opens the existing safe editor. */
export function PointTable({ detail, onEdit }: { detail: AreaDetail; onEdit: (target: EditTarget) => void }) {
  const { area, points } = detail;
  const printLink = (signId: number) => `/admin/print?area=${area.id}&sign=${signId}`;

  return (
    <section className="flex flex-col overflow-hidden rounded-lg border border-border bg-surface">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3">
        <h3 className="text-base font-bold">{`จุดในพื้นที่ · ${points.length}`}</h3>
        {area.isActive && (
          <Button size="desk" onClick={() => onEdit({ kind: 'new-point' })}>
            <Plus aria-hidden="true" className="size-4" />
            เพิ่มจุด
          </Button>
        )}
      </div>
      {points.length === 0 ? (
        <PageEmpty title="พื้นที่นี้ยังไม่มีจุดทำความสะอาด">กด "เพิ่มจุด" เพื่อเริ่ม</PageEmpty>
      ) : (
        <Table label="จุดทำความสะอาดของพื้นที่นี้" framed={false}>
          <thead>
            <tr>
              <th scope="col">จุด</th>
              <th scope="col">รอบกะเช้า</th>
              <th scope="col">รอบกะดึก</th>
              <th scope="col">รัศมี</th>
              <th scope="col">พิกัด</th>
              {/* Keep the absolutely positioned label inside the table's scroll box. */}
              <th scope="col" className="relative">
                <span className="sr-only">การกระทำ</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {points.map((point) => (
              <tr key={point.id} className={point.isActive ? undefined : 'text-text-2'}>
                <th scope="row" className="font-normal">
                  <span className="flex flex-col">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold">{point.name}</span>
                      {!point.isActive && (
                        <span className="inline-flex min-h-6 items-center rounded-sm bg-idle-bg px-1.5 text-xs font-semibold whitespace-nowrap text-idle">
                          ปิดใช้งาน
                        </span>
                      )}
                    </span>
                    <span className="font-mono text-xs text-text-2">{point.sign.code}</span>
                  </span>
                </th>
                <td className="tabular-nums">{shiftRounds(point, 'Day')}</td>
                <td className="tabular-nums">{shiftRounds(point, 'Night')}</td>
                <td className="whitespace-nowrap tabular-nums">{`${point.sign.radiusM} ม.`}</td>
                <td>
                  <SignPlace sign={point.sign} />
                </td>
                <td className="w-px text-right whitespace-nowrap">
                  <div className="inline-flex items-center gap-2">
                    <Button
                      variant="link"
                      size="desk"
                      aria-label={`แก้ไข ${point.sign.code}`}
                      onClick={() => onEdit({ kind: 'point', pointId: point.id })}
                    >
                      <Pencil aria-hidden="true" className="size-4" />
                      แก้
                    </Button>
                    {area.isActive && point.isActive && (
                      <Button variant="link" size="desk" aria-label={`จัดการ QR ${point.sign.code}`}
                        onClick={() => onEdit({ kind: 'point', pointId: point.id })}>
                        <QrCode aria-hidden="true" className="size-4" />
                        จัดการ QR
                      </Button>
                    )}
                    {area.isActive && point.isActive && (
                      <Link className={buttonClass('link', 'desk')} data-tap="" to={printLink(point.sign.id)}>
                        พิมพ์
                      </Link>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
      <p className="border-t border-border px-4 py-3 text-xs text-text-2">
        สร้าง QR ใหม่ ทำให้ป้ายเก่าสแกนไม่ได้ทันที ต้องพิมพ์และติดป้ายใหม่ · เก็บพิกัดด้วยมือถือที่ป้าย
      </p>
    </section>
  );
}
