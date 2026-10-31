import { useSetAccountActiveMutation, type Account } from './accountsApi';
export function useDeactivateConfirm(account: Account, onDone: (text: string) => void) {
  const [setActive] = useSetAccountActiveMutation();
  return async () => {
    await setActive({ id: account.id, active: false }).unwrap();
    onDone(`ปิดใช้งาน ${account.displayName} แล้ว`);
  };
}
