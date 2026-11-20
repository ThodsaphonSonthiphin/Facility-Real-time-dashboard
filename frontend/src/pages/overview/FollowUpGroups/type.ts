import type { OverviewGroup } from '../../../shared/overview/types';

export interface FollowUpGroupItem {
  key: OverviewGroup;
  label: string;
  text: string;
  percent: number | null;
  selected: boolean;
  tone: 'overdue' | 'pending' | 'rework';
}

export interface FollowUpGroupsProps {
  title: string;
  subtitle: string;
  max: number;
  items: readonly FollowUpGroupItem[];
  onGroup: (group: OverviewGroup) => void;
}
