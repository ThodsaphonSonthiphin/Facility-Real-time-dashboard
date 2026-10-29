import { LogOut } from 'lucide-react';
import { useAppDispatch } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { Button } from './Button';

/**
 * "ออกจากระบบ" as the design file's text button: accent words and the door icon, centred at the end of a phone page.
 * The phone pages have no shared top bar, so each page that has no other way out ends with this (My Work, My Building,
 * the Admin's sign capture; owner, 2026-10-29).
 */
export function LogoutButton() {
  const dispatch = useAppDispatch();
  return (
    <Button variant="link" className="self-center" onClick={() => void endSession(dispatch)}>
      <LogOut aria-hidden="true" className="size-4" />
      ออกจากระบบ
    </Button>
  );
}
