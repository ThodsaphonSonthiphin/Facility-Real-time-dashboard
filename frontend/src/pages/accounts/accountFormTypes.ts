import type { AreaSummary, Building } from '../../shared/api/adminSetupApi';
import type { Account, AccountCleanerKind } from './accountsApi';

export interface CreateAccountFormProps {
  accounts: Account[];
  areas: AreaSummary[];
  buildings: Building[];
  /** Given: "ยกเลิก" closes the Drawer it sits in. */
  onCancel: () => void;
}

export interface EditAccountFormProps {
  account: Account;
  accounts: Account[];
  areas: AreaSummary[];
  buildings: Array<Building & { isActive?: boolean }>;
  onDone: (text: string) => void;
  onCancel: () => void;
}

export interface SecretFormProps {
  account: Account;
  onDone: (text: string) => void;
  onCancel: () => void;
}

export interface CleanerKindFields {
  cleanerKind?: AccountCleanerKind;
  homeBuildingId?: string;
}
