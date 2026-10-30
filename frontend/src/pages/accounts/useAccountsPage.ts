import { useState, type ChangeEvent } from 'react';
import { useGetAreasQuery, useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppSelector } from '../../store/hooks';
import { filterAccounts, filterCounts, type AccountActiveFilter } from './accountRules';
import { useGetAccountsQuery, useSetAccountActiveMutation, type Account, type AccountRole } from './accountsApi';
import type { AccountAction, AccountNotice, AccountPanel } from './type';

/** Keeps role, active-state and search independent; forms still receive every account for assignment guards. */
export function useAccountsPage() {
  const accounts = useGetAccountsQuery(undefined, freshOnOpen);
  const areas = useGetAreasQuery(undefined, freshOnOpen);
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const me = useAppSelector((state) => state.auth.currentUser?.id);
  const [role, setRole] = useState<AccountRole>('Cleaner');
  const [active, setActive] = useState<AccountActiveFilter>('all');
  const [search, setSearch] = useState('');
  const changeActive = (event: ChangeEvent<HTMLSelectElement>) => setActive(event.target.value as AccountActiveFilter);
  const changeSearch = (event: ChangeEvent<HTMLInputElement>) => setSearch(event.target.value);
  const [panel, setPanel] = useState<AccountPanel>(null);
  const [notice, setNotice] = useState<AccountNotice>(null);
  const [setAccountActive, activating] = useSetAccountActiveMutation();
  const list = accounts.data ?? [];
  const shown = filterAccounts(list, role, active, search);
  const counts = filterCounts(list);
  const openCreate = () => setPanel({ kind: 'create' });
  const error = accounts.error ?? areas.error ?? buildings.error;
  const target = panel === null || panel.kind === 'create' ? undefined : list.find((account) => account.id === panel.id);
  const closePanel = () => setPanel(null);
  const done = (text: string) => {
    setNotice({ kind: 'ok', text });
    closePanel();
  };

  // facility-0020: activation is one press; a Cleaner comes back with no Area (plan 5 decision 3)
  const activate = async (account: Account) => {
    try {
      await setAccountActive({ id: account.id, active: true }).unwrap();
      setNotice({
        kind: 'ok',
        text:
          account.role === 'Cleaner'
            ? `เปิดใช้งาน ${account.displayName} แล้ว ยังไม่มีพื้นที่ เลือกพื้นที่ได้ที่ "แก้ไข"`
            : `เปิดใช้งาน ${account.displayName} แล้ว`,
      });
    } catch (activationError) {
      setNotice({ kind: 'error', text: getErrorMessage(activationError) });
    }
  };

  const act = (action: AccountAction, account: Account) => {
    setNotice(null);
    if (action === 'activate') {
      void activate(account);
      return;
    }
    setPanel({ kind: action, id: account.id });
  };

  return {
    accounts, areas, buildings, me, role, setRole, active, changeActive, search, changeSearch,
    panel, notice, list, shown, counts, error, target, closePanel, done, act, openCreate,
    busy: activating.isLoading,
  };
}
