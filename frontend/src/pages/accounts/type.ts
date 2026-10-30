import type { Account, AccountRole } from './accountsApi';

export type AccountAction = 'edit' | 'phone' | 'password' | 'deactivate' | 'activate';
export type AccountPanel = { kind: 'create' } | { kind: Exclude<AccountAction, 'activate'>; id: number } | null;
export type AccountNotice = { kind: 'ok' | 'error'; text: string } | null;

export interface AccountTableProps {
  accounts: Account[];
  role: AccountRole;
  /** facility-0020: an Admin cannot deactivate their own account. */
  me: number | undefined;
  busy: boolean;
  onAction: (action: AccountAction, account: Account) => void;
}
