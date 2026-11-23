import type { AvatarBadgeProps, AvatarBadgeSize, AvatarBadgeTone } from './type';

const toneClasses: Record<AvatarBadgeTone, string> = {
  teal: 'bg-pending-bg text-pending',
  amber: 'bg-overdue-bg text-overdue',
  purple: 'bg-break-bg text-break',
  neutral: 'bg-surface-2 text-text-3',
};

const sizeClasses: Record<AvatarBadgeSize, string> = {
  sm: 'size-7 text-xs',
  md: 'size-9 text-sm',
};

// Leading Thai vowels (สระหน้า: เ แ โ ใ ไ) precede the initial consonant in Unicode order.
const THAI_LEADING_VOWEL_WITH_CONSONANT = /^[\u0E40-\u0E44]([\u0E01-\u0E2E])/;

/**
 * Extracts a single display initial from a Thai or Latin name.
 */
function extractInitial(name: string): string {
  const trimmed = name.trim();
  if (!trimmed) return '?';
  const thaiMatch = THAI_LEADING_VOWEL_WITH_CONSONANT.exec(trimmed);
  if (thaiMatch) return thaiMatch[1];
  return trimmed.charAt(0).toUpperCase();
}

/**
 * Decorative rounded-square initial badge hidden from assistive technology (`aria-hidden="true"`).
 */
export function AvatarBadge({ name, tone = 'neutral', size = 'md', className }: AvatarBadgeProps) {
  return (
    <span
      aria-hidden="true"
      className={`inline-flex shrink-0 items-center justify-center rounded-md border border-border font-bold ${toneClasses[tone]} ${sizeClasses[size]}${className ? ` ${className}` : ''}`}
    >
      {extractInitial(name)}
    </span>
  );
}
