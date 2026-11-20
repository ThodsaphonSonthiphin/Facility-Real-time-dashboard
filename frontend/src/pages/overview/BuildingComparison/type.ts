import type { OverviewGroup } from '../../../shared/overview/types';

export interface BuildingMetricCell {
  group: 'entered' | 'submitted' | 'inspected';
  label: string;
  text: string;
  percent: number | null;
  tone: 'passed' | 'pending';
  ariaLabel: string;
  selected: boolean;
}

export interface BuildingComparisonRow {
  id: number;
  label: string;
  cells: readonly BuildingMetricCell[];
}

export interface BuildingComparisonProps {
  title: string;
  rows: readonly BuildingComparisonRow[];
  emptyText?: string;
  onSelect: (buildingId: number, group: OverviewGroup) => void;
}
