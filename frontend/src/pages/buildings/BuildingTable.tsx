import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { statusText } from './buildingRules';

interface BuildingTableProps {
  buildings: ManagedBuilding[];
  onEdit: (building: ManagedBuilding) => void;
}

/**
 * facility-0090 decision 1: every building in the server's order (by code); an off one is faded and says so in words.
 * Each button names its building, so a screen reader and the tests can tell the rows apart.
 */
export function BuildingTable({ buildings, onEdit }: BuildingTableProps) {
  return (
    <Table label="ตึก">
      <thead>
        <tr>
          <th scope="col">รหัส</th>
          <th scope="col">ชื่อ</th>
          <th scope="col">พื้นที่ที่เปิดอยู่</th>
          <th scope="col">สถานะ</th>
          <th scope="col">จัดการ</th>
        </tr>
      </thead>
      <tbody>
        {buildings.map((building) => (
          <tr key={building.id} className={building.isActive ? undefined : 'text-muted'}>
            <th scope="row">{building.code}</th>
            <td>{building.name}</td>
            <td>{building.activeAreaCount}</td>
            <td>{statusText(building)}</td>
            <td>
              <Button variant="link" size="desk" aria-label={`แก้ไข ตึก ${building.code}`} onClick={() => onEdit(building)}>
                แก้ไข
              </Button>
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
