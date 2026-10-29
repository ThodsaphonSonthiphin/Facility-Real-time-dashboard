import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { useBuildingNames } from '../../shared/api/buildingNames';
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
  const names = useBuildingNames();
  if (accounts.length === 0) {
    return <Note tone="info">ไม่พบบัญชีที่ตรงกับที่ค้นหา</Note>;
  }
  return (
    <Table label="บัญชีผู้ใช้">
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
          <tr key={account.id} className={account.isActive ? undefined : 'text-text-2'}>
            <th scope="row">{account.displayName}</th>
            <td>{account.loginName}</td>
            <td>{accountRoleLabel[account.role]}</td>
            <td>{placeText(account, names)}</td>
            <td>{account.isActive ? 'ใช้งาน' : 'ปิดใช้งาน'}</td>
            <td>
              <div className="flex items-center gap-3 whitespace-nowrap">
                <Button
                  variant="link"
                  size="desk"
                  aria-label={`แก้ไข ${account.displayName}`}
                  onClick={() => onAction('edit', account)}
                >
                  แก้ไข
                </Button>
                {account.role === 'Admin' ? (
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`รีเซ็ตรหัสผ่าน ${account.displayName}`}
                    onClick={() => onAction('password', account)}
                  >
                    รีเซ็ตรหัสผ่าน
                  </Button>
                ) : (
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`แก้เบอร์โทร ${account.displayName}`}
                    onClick={() => onAction('phone', account)}
                  >
                    แก้เบอร์โทร
                  </Button>
                )}
                {account.isActive ? (
                  account.id !== me && (
                    <Button
                      variant="link"
                      size="desk"
                      aria-label={`ปิดใช้งาน ${account.displayName}`}
                      onClick={() => onAction('deactivate', account)}
                    >
                      ปิดใช้งาน
                    </Button>
                  )
                ) : (
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`เปิดใช้งาน ${account.displayName}`}
                    disabled={busy}
                    onClick={() => onAction('activate', account)}
                  >
                    เปิดใช้งาน
                  </Button>
                )}
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
