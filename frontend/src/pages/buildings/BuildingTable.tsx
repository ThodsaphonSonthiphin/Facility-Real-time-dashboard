import { Pencil } from 'lucide-react';
import { ActiveTag } from '../../shared/components/ActiveTag';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { statusText } from './buildingRules';
import type { BuildingTableProps } from './type';

/**
 * facility-0090 decision 1: every building in the server's order (by code); an off one is faded and says so in words
 * (the design file's D15 table). Each button names its building, so a screen reader and the tests can tell the rows
 * apart.
 */
export function BuildingTable({ buildings, supervisors, supervisorState, onEdit }: BuildingTableProps) {
  return (
    <Table label="ตึก" framed={false}>
      <thead>
        <tr>
          <th scope="col" className="w-20 px-4">รหัส</th>
          <th scope="col">ชื่อตึก</th>
          <th scope="col" className="text-right">
            พื้นที่
          </th>
          <th scope="col" className="w-48">หัวหน้า</th>
          <th scope="col" className="w-37 text-center">
            สถานะ
          </th>
          {/* Keep the absolutely positioned label inside the table's scroll box. */}
          <th scope="col" className="relative">
            <span className="sr-only">การกระทำ</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {buildings.map((building) => (
          <tr key={building.id} className={building.isActive ? undefined : 'bg-surface-2 text-text-2'}>
            <th scope="row" className="px-4 text-base font-bold">
              {building.code}
            </th>
            <td className="font-semibold">{building.name}</td>
            <td className="text-right tabular-nums">{building.activeAreaCount}</td>
            <td>
              {supervisorState === 'loading' ? (
                <span className="text-text-2">กำลังโหลดหัวหน้า...</span>
              ) : supervisorState === 'error' ? (
                <span className="text-text-2">โหลดรายชื่อไม่ได้</span>
              ) : (supervisors.get(building.id)?.length ?? 0) > 0 ? (
                  <div className="flex flex-col gap-0.5">
                    {supervisors.get(building.id)?.map((group) => (
                      <span key={group.shift}>{group.shift} · {group.names}</span>
                    ))}
                  </div>
              ) : <span className="text-text-2">ยังไม่มีหัวหน้า</span>}
            </td>
            <td className="text-center">
              {/* statusText is the on-state words ("ใช้งาน"); the tag's own words say "ปิดใช้งาน" when off */}
              <span className="inline-flex w-31 justify-center [&>span]:w-full [&>span]:justify-center">
                <ActiveTag active={building.isActive} on={statusText(building)} />
              </span>
            </td>
            <td className="w-px text-right whitespace-nowrap">
              <Button variant="link" size="desk" aria-label={`แก้ไข ตึก ${building.code}`} onClick={() => onEdit(building)}>
                <Pencil aria-hidden="true" className="size-4" />
                แก้ไข
              </Button>
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
