import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import type { Shift } from '../../shared/api/types';

export type OpenBuilding = { kind: 'create' } | { kind: 'edit'; id: number } | null;

/** Fields consumed from AccountDto, with no private fields. */
export interface BuildingSupervisor {
  id: number;
  role: string;
  displayName: string;
  buildingId?: number;
  shift?: Shift;
  isActive: boolean;
}

export interface SupervisorGroup {
  shift: string;
  names: string;
}

export interface BuildingTableProps {
  buildings: ManagedBuilding[];
  supervisors: Map<number, SupervisorGroup[]>;
  supervisorState: 'loading' | 'error' | 'ready';
  onEdit: (building: ManagedBuilding) => void;
}
