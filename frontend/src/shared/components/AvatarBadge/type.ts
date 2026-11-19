/** Semantic color tone for an employee or user initial badge. */
export type AvatarBadgeTone = 'blue' | 'teal' | 'amber' | 'purple' | 'neutral';

/** Size variant for an AvatarBadge. */
export type AvatarBadgeSize = 'sm' | 'md';

/** Props for the decorative AvatarBadge component. */
export interface AvatarBadgeProps {
  name: string;
  tone?: AvatarBadgeTone;
  size?: AvatarBadgeSize;
  className?: string;
}
