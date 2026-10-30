import { useState } from 'react';
import { useGetAllBuildingsQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import type { Notice } from '../../shared/components/Toast';
import { useGetBuildingSupervisorsQuery } from './buildingsApi';
import type { OpenBuilding, SupervisorGroup } from './type';

export function useBuildingsPage() {
  const buildings = useGetAllBuildingsQuery(undefined, freshOnOpen);
  const accounts = useGetBuildingSupervisorsQuery(undefined, freshOnOpen);
  const [open, setOpen] = useState<OpenBuilding>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const list = buildings.data ?? [];
  const editing = open?.kind === 'edit' ? list.find((building) => building.id === open.id) : undefined;
  const supervisors = new Map<number, SupervisorGroup[]>();
  for (const building of list) {
    const assigned = accounts.data?.filter(
      (account) => account.role === 'Supervisor' && account.isActive && account.buildingId === building.id,
    ) ?? [];
    supervisors.set(building.id, [
      { shift: 'เช้า', names: assigned.filter((account) => account.shift === 'Day').map((account) => account.displayName).join(' · ') },
      { shift: 'ดึก', names: assigned.filter((account) => account.shift === 'Night').map((account) => account.displayName).join(' · ') },
    ].filter((group) => group.names !== ''));
  }
  const supervisorState = accounts.isFetching ? 'loading' : accounts.error ? 'error' : 'ready';
  const close = () => setOpen(null);
  const done = (text: string) => {
    setOpen(null);
    setNotice({ kind: 'ok', text });
  };

  return {
    buildings, accounts, list, supervisors, supervisorState, open, editing, notice, close, done,
    create: () => setOpen({ kind: 'create' }),
    edit: (building: { id: number }) => setOpen({ kind: 'edit', id: building.id }),
    dismissNotice: () => setNotice(null),
  } as const;
}
