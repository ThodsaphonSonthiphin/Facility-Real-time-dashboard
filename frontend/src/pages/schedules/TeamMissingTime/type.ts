import type { TeamSlotArgs } from '../schedulesApi';

export interface TeamMissingTimeProps {
  slot: TeamSlotArgs;
  displayName: string;
  phone: boolean;
  onClose: () => void;
}
