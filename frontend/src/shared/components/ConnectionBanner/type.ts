import type { HubStatus } from '../../../store/slices/hubSlice';

export interface ConnectionBannerProps {
  status: HubStatus;
  /** Optional extra stale flag (e.g. schedule read behind update). */
  stale?: boolean;
  /** True while a manual refresh request is in flight. */
  refreshing?: boolean;
  /** Triggered when the user taps the inline retry button. */
  onRetry: () => void;
}
