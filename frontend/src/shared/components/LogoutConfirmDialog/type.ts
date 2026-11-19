export interface LogoutConfirmDialogProps {
  /** Called when the user cancels or presses Escape. */
  onCancel: () => void;
  /** Called when the user confirms logout. */
  onConfirm: () => void;
}
