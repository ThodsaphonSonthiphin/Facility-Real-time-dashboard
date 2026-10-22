import { useState } from 'react';
import { useGetAreasQuery, useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppSelector } from '../../store/hooks';
import { accountFilters, filterAccounts, filterCounts, filterLabel, type AccountFilter } from './accountRules';
import { useGetAccountsQuery, useSetAccountActiveMutation, type Account } from './accountsApi';
import { AccountTable, type AccountAction } from './AccountTable';
import { CreateAccountForm } from './CreateAccountForm';
import { DeactivateConfirm } from './DeactivateConfirm';
import { EditAccountForm } from './EditAccountForm';
import { PasswordForm, PhoneForm } from './SecretForms';
import './accounts.css';

type Panel = { kind: 'create' } | { kind: 'edit' | 'phone' | 'password' | 'deactivate'; id: number };

type Notice = { kind: 'ok' | 'error'; text: string } | null;

/**
 * facility-0020, 0056, wireframe D6: every account, filtered and searched on the page. The side panel adds an account,
 * or holds the action of the row pressed; when that action is done it goes back to "เพิ่มบัญชี".
 */
export function AccountsPage() {
  const accounts = useGetAccountsQuery(undefined, freshOnOpen);
  const areas = useGetAreasQuery(undefined, freshOnOpen);
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const me = useAppSelector((state) => state.auth.currentUser?.id);
  const [filter, setFilter] = useState<AccountFilter>('all');
  const [search, setSearch] = useState('');
  const [panel, setPanel] = useState<Panel>({ kind: 'create' });
  const [notice, setNotice] = useState<Notice>(null);
  const [setAccountActive, activating] = useSetAccountActiveMutation();
  const list = accounts.data ?? [];
  const counts = filterCounts(list);
  const error = accounts.error ?? areas.error ?? buildings.error;
  const target = panel.kind === 'create' ? undefined : list.find((account) => account.id === panel.id);
  const backToCreate = () => setPanel({ kind: 'create' });
  const done = (text: string) => {
    setNotice({ kind: 'ok', text });
    backToCreate();
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
          {notice && (
            <p
              className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
              role={notice.kind === 'ok' ? 'status' : 'alert'}
            >
              {notice.text}
            </p>
          )}
          {accounts.isLoading && <p className="app-muted">กำลังโหลดบัญชี...</p>}
          {accounts.data && (
            <AccountTable
              accounts={filterAccounts(list, filter, search)}
              me={me}
              busy={activating.isLoading}
              onAction={act}
            />
          )}
          <p className="app-muted">
            ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติการสแกนยังอยู่ · ปิดใช้งาน แก้เบอร์โทร หรือรีเซ็ตรหัสผ่านแล้ว
            ทุกเครื่องของคนนั้นหลุดภายใน 5 นาที · ปิดบัญชีของตัวเองไม่ได้ · ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
          </p>
        </section>
        <aside className="accounts-panel" aria-label="แผงบัญชี">
          {!target && <CreateAccountForm accounts={list} areas={areas.data ?? []} buildings={buildings.data ?? []} />}
          {target && panel.kind === 'edit' && (
            <EditAccountForm
              key={target.id}
              account={target}
              accounts={list}
              areas={areas.data ?? []}
              buildings={buildings.data ?? []}
              onDone={done}
              onCancel={backToCreate}
            />
          )}
          {target && panel.kind === 'phone' && (
            <PhoneForm key={target.id} account={target} onDone={done} onCancel={backToCreate} />
          )}
          {target && panel.kind === 'password' && (
            <PasswordForm key={target.id} account={target} isSelf={target.id === me} onDone={done} onCancel={backToCreate} />
          )}
          {target && panel.kind === 'deactivate' && (
            <DeactivateConfirm key={target.id} account={target} onDone={done} onCancel={backToCreate} />
          )}
        </aside>
      </div>
    </main>
  );
}
