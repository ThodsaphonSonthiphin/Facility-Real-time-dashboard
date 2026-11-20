import type { OverviewGroup } from '../../../shared/overview/types';

export interface FollowUpGroupItem {
  key: OverviewGroup;
  label: string;
  text: string;
  selected: boolean;
  tone: 'overdue' | 'pending' | 'rework';
}

export interface FollowUpGroupsProps {
  title: string;
  subtitle: string;
  items: readonly FollowUpGroupItem[];
  onGroup: (group: OverviewGroup) => void;
}
