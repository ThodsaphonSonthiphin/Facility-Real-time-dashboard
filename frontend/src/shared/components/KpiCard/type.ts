import type { LucideIcon } from 'lucide-react';

/** Semantic color tone for a KPI summary card. */
export type KpiCardTone = 'teal' | 'green' | 'amber' | 'red' | 'purple' | 'neutral';

/** Props for the reusable KPI summary card component. */
export interface KpiCardProps {
  label: string;
  value: number | string;
  unit?: string;
  icon: LucideIcon;
  tone?: KpiCardTone;
  /** Optional completion percentage (0–100); renders a progressbar when defined. */
  progress?: number;
  /** Accessible label for the progressbar; defaults to `label`. */
  progressLabel?: string;
  /** Element tag; `'li'` preserves `item.textContent === `${label}${formattedValue}`` when `unit` and `progress` are omitted. */
  as?: 'div' | 'li';
  className?: string;
}
