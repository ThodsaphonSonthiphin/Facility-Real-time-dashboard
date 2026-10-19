import { useState } from 'react';
import { useGetAreasQuery, useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { accountFilters, filterAccounts, filterCounts, filterLabel, type AccountFilter } from './accountRules';
import { useGetAccountsQuery } from './accountsApi';
import { AccountTable } from './AccountTable';
import { CreateAccountForm } from './CreateAccountForm';
import './accounts.css';

/** facility-0020, 0056, wireframe D6: every account, filtered and searched on the page, and the add form beside it. */
export function AccountsPage() {
  const accounts = useGetAccountsQuery();
  const areas = useGetAreasQuery();
  const buildings = useGetBuildingsQuery();
  const [filter, setFilter] = useState<AccountFilter>('all');
  const [search, setSearch] = useState('');
  const list = accounts.data ?? [];
  const counts = filterCounts(list);
  const error = accounts.error ?? areas.error ?? buildings.error;

  return (
    <main className="accounts-page">
      <h1>บัญชีผู้ใช้</h1>
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      <div className="accounts-layout">
        <section className="accounts-list" aria-label="รายชื่อบัญชี">
          <div className="accounts-filters" role="group" aria-label="กรองบัญชี">
            {accountFilters.map((option) => (
              <button
                key={option}
                type="button"
                className={filter === option ? 'accounts-filter accounts-filter--on' : 'accounts-filter'}
                aria-pressed={filter === option}
                onClick={() => setFilter(option)}
              >
                {`${filterLabel[option]} (${counts[option]})`}
              </button>
            ))}
          </div>
          <label className="app-field accounts-search">
            ค้นหา
            <input
              type="search"
              className="app-input"
              placeholder="ชื่อ รหัสพนักงาน หรือชื่อผู้ใช้"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          {accounts.isLoading && <p className="app-muted">กำลังโหลดบัญชี...</p>}
          {accounts.data && <AccountTable accounts={filterAccounts(list, filter, search)} />}
          <p className="app-muted">
            ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติการสแกนยังอยู่ · ปิดใช้งาน แก้เบอร์โทร หรือรีเซ็ตรหัสผ่านแล้ว
            ทุกเครื่องของคนนั้นหลุดภายใน 5 นาที · ปิดบัญชีของตัวเองไม่ได้ · ทุกการแก้ในหน้านี้เก็บลงประวัติของ Admin
          </p>
        </section>
        <aside className="accounts-panel" aria-label="แผงบัญชี">
          <CreateAccountForm accounts={list} areas={areas.data ?? []} buildings={buildings.data ?? []} />
        </aside>
      </div>
    </main>
  );
}
