import { accountRoleLabel, placeText } from './accountRules';
import type { Account } from './accountsApi';

export type AccountAction = 'edit' | 'phone' | 'password' | 'deactivate' | 'activate';

interface AccountTableProps {
  accounts: Account[];
  /** The logged-in Admin's id: they cannot deactivate themselves (facility-0020). */
  me: number | undefined;
  /** An activation is in flight. */
  busy: boolean;
  onAction: (action: AccountAction, account: Account) => void;
}

/**
 * Wireframe D6's table, in the server's order (Admins, Supervisors, Cleaners); deactivated rows are faded. Each
 * button names its person, so a screen reader and the tests can tell the rows apart.
 */
export function AccountTable({ accounts, me, busy, onAction }: AccountTableProps) {
  if (accounts.length === 0) {
    return <p className="app-note app-note--info">ไม่พบบัญชีที่ตรงกับที่ค้นหา</p>;
  }
  return (
    <table className="app-table" aria-label="บัญชีผู้ใช้">
      <thead>
        <tr>
          <th scope="col">ชื่อ</th>
          <th scope="col">รหัสพนักงาน / ชื่อผู้ใช้</th>
          <th scope="col">บทบาท</th>
          <th scope="col">ประจำที่</th>
          <th scope="col">ใช้งาน</th>
          <th scope="col">จัดการ</th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <tr key={account.id} className={account.isActive ? undefined : 'app-row--inactive'}>
            <th scope="row">{account.displayName}</th>
            <td>{account.loginName}</td>
            <td>{accountRoleLabel[account.role]}</td>
            <td>{placeText(account)}</td>
            <td>{account.isActive ? 'ใช้งาน' : 'ปิดใช้งาน'}</td>
            <td className="accounts-actions">
              <button
                type="button"
                className="app-link-button"
                aria-label={`แก้ไข ${account.displayName}`}
                onClick={() => onAction('edit', account)}
              >
                แก้ไข
              </button>
              {account.role === 'Admin' ? (
                <button
                  type="button"
                  className="app-link-button"
                  aria-label={`รีเซ็ตรหัสผ่าน ${account.displayName}`}
                  onClick={() => onAction('password', account)}
                >
                  รีเซ็ตรหัสผ่าน
                </button>
              ) : (
                <button
                  type="button"
                  className="app-link-button"
                  aria-label={`แก้เบอร์โทร ${account.displayName}`}
                  onClick={() => onAction('phone', account)}
                >
                  แก้เบอร์โทร
                </button>
              )}
              {account.isActive
                ? account.id !== me && (
                    <button
                      type="button"
                      className="app-link-button"
                      aria-label={`ปิดใช้งาน ${account.displayName}`}
                      onClick={() => onAction('deactivate', account)}
                    >
                      ปิดใช้งาน
                    </button>
                  )
                : (
                    <button
                      type="button"
                      className="app-link-button"
                      aria-label={`เปิดใช้งาน ${account.displayName}`}
                      disabled={busy}
                      onClick={() => onAction('activate', account)}
                    >
                      เปิดใช้งาน
                    </button>
                  )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
