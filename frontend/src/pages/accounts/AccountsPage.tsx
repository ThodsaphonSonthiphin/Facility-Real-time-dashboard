import { Plus } from 'lucide-react';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { Tabs } from '../../shared/components/Tabs';
import { Toast } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { accountRoles, accountRoleLabel } from './accountRules';
import { AccountTable } from './AccountTable';
import { CreateAccountForm } from './CreateAccountForm';
import { DeactivateConfirm } from './DeactivateConfirm';
import { EditAccountForm } from './EditAccountForm';
import { useAccountsPage } from './useAccountsPage';

/** Counted role views of the same account list; forms retain the full data and existing business rules. */
export function AccountsPage() {
  const {
    accounts, areas, buildings, me, role, setRole, active, changeActive, search, changeSearch,
    panel, notice, clearNotice, list, shown, counts, error, target, closePanel, done, act, openCreate, busy,
  } = useAccountsPage();

  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        title="บัญชีผู้ใช้"
        actions={
          <Button variant="primary" size="adaptive" onClick={openCreate}>
            <Plus aria-hidden="true" className="size-4" />
            สร้างบัญชี
          </Button>
        }
      />
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {notice && <Toast notice={notice} onClose={clearNotice} />}
      <section
        className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-border bg-surface"
        aria-label="รายชื่อบัญชี"
      >
        <div className="flex flex-col border-b border-border lg:flex-row lg:items-end lg:justify-between lg:gap-x-4 lg:px-4 lg:pt-1">
          <div className="border-b border-border px-3 pt-1 lg:border-b-0 lg:px-0 lg:pt-0">
            <Tabs
              label="บทบาทบัญชี"
              idPrefix="accounts"
              ruled={false}
              value={role}
              onChange={setRole}
              tabs={accountRoles.map((id) => ({
                id,
                label: (
                  <>
                    {accountRoleLabel[id]}{' '}
                    <span className={id === role
                      ? 'inline-flex min-h-5.5 min-w-5.5 items-center justify-center rounded-sm bg-accent-subtle px-1.5 text-xs text-accent'
                      : 'inline-flex min-h-5.5 min-w-5.5 items-center justify-center rounded-sm bg-idle-bg px-1.5 text-xs text-idle'}>
                      {counts[id]}
                    </span>
                  </>
                ),
              }))}
            />
          </div>
          <div className="grid grid-cols-1 gap-3 bg-surface-2 p-3 sm:grid-cols-[180px_minmax(0,1fr)] lg:flex lg:w-auto lg:bg-transparent lg:p-0 lg:py-3">
            <Field label="สถานะบัญชี">
              <Select size="adaptive" className="w-full" value={active} onChange={changeActive}>
                <option value="all">ทั้งหมด</option>
                <option value="active">ใช้งานอยู่</option>
                <option value="inactive">ปิดใช้งาน</option>
              </Select>
            </Field>
            <div className="min-w-0 lg:w-60">
              <Field label="ค้นหา">
                <TextInput
                  size="adaptive"
                  type="search"
                  className="w-full"
                  placeholder="ชื่อ รหัสพนักงาน หรือชื่อผู้ใช้"
                  value={search}
                  onChange={changeSearch}
                />
              </Field>
            </div>
          </div>
        </div>
        <div role="tabpanel" id={`accounts-panel-${role}`} aria-labelledby={`accounts-tab-${role}`}>
          {accounts.isLoading && (
            <div className="px-4 py-3">
              <PageLoading>กำลังโหลดบัญชี...</PageLoading>
            </div>
          )}
          {accounts.data && <AccountTable accounts={shown} role={role} me={me} busy={busy} onAction={act} />}
          {accounts.data && (
            <div className="border-t border-border px-4 py-2.5 text-text-2">
              {`แสดง ${shown.length > 0 ? `1–${shown.length}` : '0'} จาก ${counts[role]} บัญชี`}
            </div>
          )}
        </div>
      </section>
      {panel?.kind === 'create' && (
        <Drawer width="lg" labelledBy="accounts-create-title" onClose={closePanel}>
          <CreateAccountForm
            accounts={list}
            areas={areas.data ?? []}
            buildings={(buildings.data ?? []).filter((building) => building.isActive)}
            onCancel={closePanel}
          />
        </Drawer>
      )}
      {target && panel?.kind === 'edit' && (
        <Drawer width="lg" labelledBy={`accounts-edit-${target.id}`} onClose={closePanel}>
          <EditAccountForm
            key={target.id}
            account={target}
            accounts={list}
            areas={areas.data ?? []}
            buildings={buildings.data ?? []}
            onDone={done}
            onCancel={closePanel}
          />
        </Drawer>
      )}
      {target && panel?.kind === 'deactivate' && (
        <DeactivateConfirm key={target.id} account={target} onDone={done} onCancel={closePanel} />
      )}
    </main>
  );
}
