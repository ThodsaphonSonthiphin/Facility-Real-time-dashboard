import { Check, MapPin } from 'lucide-react';
import type { AdminSign } from '../../shared/api/adminSetupApi';
import { signPlaceLabel } from '../../shared/utils/labels';

/**
 * A sign's place in words, with its icon (the design file's D4): a green tick once it was measured at the sign itself,
 * a grey pin for a place picked on a map or not recorded yet. The words always say which.
 */
export function SignPlace({ sign }: { sign: AdminSign }) {
  const captured = sign.locationSource === 'Site';
  return (
    <span className="inline-flex items-center gap-1">
      {captured ? (
        <Check aria-hidden="true" className="size-4 shrink-0 text-passed" />
      ) : (
        <MapPin aria-hidden="true" className="size-4 shrink-0 text-text-2" />
      )}
      {signPlaceLabel(sign)}
    </span>
  );
}
