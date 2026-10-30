import { Check, KeyRound, Minus, Pencil, Phone, Power } from 'lucide-react';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { placeText } from './accountRules';
import type { AccountTableProps } from './type';

/** The selected role sets the identifier and place headers. All existing row operations retain their accessible names. */
export function AccountTable({ accounts, role, me, busy, onAction }: AccountTableProps) {
  const names = useBuildingNames();
  if (accounts.length === 0) {
    return <Note tone="info">ไม่พบบัญชีที่ตรงกับที่ค้นหา</Note>;
  }
  return (
    <Table label="บัญชีผู้ใช้" framed={false}>
      <thead>
        <tr>
          <th scope="col">ชื่อ</th>
          <th scope="col">{role === 'Admin' ? 'ชื่อผู้ใช้' : 'รหัสพนักงาน'}</th>
          <th scope="col">{role === 'Cleaner' ? 'พื้นที่ · กะ' : 'ตึก · กะ'}</th>
          <th scope="col" className="w-37 text-center">
            สถานะ
          </th>
          {/* Keep the absolutely positioned label inside the table's scroll box. */}
          <th scope="col" className="relative">
            <span className="sr-only">การกระทำ</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {accounts.map((account) => (
          <tr key={account.id} className={account.isActive ? undefined : 'bg-surface-2 text-text-2'}>
            <th scope="row" className="font-semibold">
              {account.displayName}
            </th>
            <td className="font-mono text-xs">{account.loginName}</td>
            <td>{placeText(account, names)}</td>
            <td className="text-center">
              {/* The approved account table uses a 124 px × 28 px status tag, wider than the compact setup-page tag. */}
              <span className={`inline-flex min-h-7 w-31 items-center justify-center gap-1.5 rounded-md border px-2.5 font-semibold whitespace-nowrap ${account.isActive
                ? 'border-passed-border bg-passed-bg text-passed'
                : 'border-idle-border bg-idle-bg text-idle'}`}>
                {account.isActive ? <Check aria-hidden="true" className="size-4" /> : <Minus aria-hidden="true" className="size-4" />}
                {account.isActive ? 'ใช้งานอยู่' : 'ปิดใช้งาน'}
              </span>
            </td>
            <td className="w-px text-right whitespace-nowrap">
              <div className="inline-flex items-center gap-3">
                <Button
                  variant="link"
                  size="desk"
                  aria-label={`แก้ไข ${account.displayName}`}
                  onClick={() => onAction('edit', account)}
                >
                  <Pencil aria-hidden="true" className="size-4" />
                  แก้
                </Button>
                {account.role === 'Admin' ? (
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`รีเซ็ตรหัสผ่าน ${account.displayName}`}
                    onClick={() => onAction('password', account)}
                  >
                    <KeyRound aria-hidden="true" className="size-4" />
                    รีเซ็ต
                  </Button>
                ) : (
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`แก้เบอร์โทร ${account.displayName}`}
                    onClick={() => onAction('phone', account)}
                  >
                    <Phone aria-hidden="true" className="size-4" />
                    เบอร์โทร
                  </Button>
                )}
                {account.isActive ? (
                  account.id !== me && (
                    <Button
                      variant="danger"
                      size="desk"
                      className="w-30"
                      aria-label={`ปิดใช้งาน ${account.displayName}`}
                      onClick={() => onAction('deactivate', account)}
                    >
                      <Power aria-hidden="true" className="size-4" />
                      ปิดใช้งาน
                    </Button>
                  )
                ) : (
                  <Button
                    variant="secondary"
                    size="desk"
                    className="w-30"
                    aria-label={`เปิดใช้งาน ${account.displayName}`}
                    disabled={busy}
                    onClick={() => onAction('activate', account)}
                  >
                    <Power aria-hidden="true" className="size-4" />
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
