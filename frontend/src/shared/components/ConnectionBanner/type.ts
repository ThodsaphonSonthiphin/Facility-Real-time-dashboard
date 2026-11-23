export interface ConnectionBannerProps {
  /** True while a manual refresh request is in flight. */
  refreshing?: boolean;
  /** Triggered when the user taps the inline retry button. */
  onRetry: () => void;
}
