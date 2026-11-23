export interface LogoutConfirmDialogProps {
  /** Keeps 48 px phone sizing regardless of viewport width when opened from a PhoneScreen route. */
  phone?: boolean;
  /** Called when the user cancels or presses Escape. */
  onCancel: () => void;
  /** Called when the user confirms logout. */
  onConfirm: () => void;
}
