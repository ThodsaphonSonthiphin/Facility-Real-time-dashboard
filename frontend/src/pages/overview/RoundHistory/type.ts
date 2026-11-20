import type { OverviewGroup } from '../../../shared/overview/types';

export interface RoundHistoryGroupItem {
  key: OverviewGroup;
  label: string;
  text: string;
  selected: boolean;
}

export interface RoundHistoryDetailNote {
  title: string;
  description: string;
}

export interface RoundHistoryProps {
  title: string;
  subtitle: string;
  availabilityNote?: string;
  groups: readonly RoundHistoryGroupItem[];
  details: readonly RoundHistoryDetailNote[];
  onGroup: (group: OverviewGroup) => void;
}
