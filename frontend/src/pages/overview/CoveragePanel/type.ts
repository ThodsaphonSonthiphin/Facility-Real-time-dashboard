import type { OverviewGroup } from '../../../shared/overview/types';

export type SwatchTone = 'passed' | 'pending' | 'overdue' | 'rework' | 'idle';

export interface CoverageMetricView {
  text: string;
  explanation: string;
  percent: number | null;
  tone: 'passed' | 'pending';
  denominatorText?: string;
}

export interface CoverageGroupOption {
  key: OverviewGroup;
  label: string;
  text: string;
  selected: boolean;
  tone?: SwatchTone;
}

export interface CoveragePanelProps {
  title: string;
  metric: CoverageMetricView;
  groups: CoverageGroupOption[];
  onGroup: (group: OverviewGroup) => void;
}
