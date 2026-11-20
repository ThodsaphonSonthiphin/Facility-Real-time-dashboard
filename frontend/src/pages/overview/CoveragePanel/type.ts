import type { OverviewGroup } from '../../../shared/overview/types';

export type RingTone = 'passed' | 'pending' | 'overdue';

export type SwatchTone = 'passed' | 'pending' | 'overdue' | 'rework' | 'idle';

export interface CoverageRingSegment {
  key: string;
  dashArray: string;
  dashOffset: number;
  tone: RingTone;
}

export interface CoverageMetricView {
  text: string;
  explanation: string;
  ringValueText?: string;
  denominatorText?: string;
  segments?: readonly CoverageRingSegment[];
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
