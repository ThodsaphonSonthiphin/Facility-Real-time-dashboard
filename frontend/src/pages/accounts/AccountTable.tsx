import { accountRoleLabel, placeText } from './accountRules';
import type { Account } from './accountsApi';

/** Wireframe D6's table, in the server's order (Admins, Supervisors, Cleaners); deactivated rows are faded. */
export function AccountTable({ accounts }: { accounts: Account[] }) {
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
          </tr>
        ))}
      </tbody>
    </table>
  );
}
