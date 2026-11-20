export interface AreaResultBadge {
  label: string;
  tone: 'passed' | 'pending' | 'overdue' | 'rework' | 'idle';
}

export interface AreaRoundLink {
  key: string;
  label: string;
  ariaLabel: string;
  href: string;
}

export interface AreaResultRowView {
  areaId: number;
  code: string;
  name: string;
  buildingText: string;
  entryBadge: AreaResultBadge;
  reasonBadges: readonly AreaResultBadge[];
  originsIncomplete: boolean;
  pointsText: string;
  href: string;
  viewAriaLabel: string;
  singleRoundLabel?: string;
  roundLinks: readonly AreaRoundLink[];
}

export interface AreaResultsEmptyState {
  kind: 'empty' | 'partial' | 'unavailable' | 'notStarted';
  title: string;
  detail: string;
}

export interface AreaResultsProps {
  title: string;
  contextText: string;
  countText: string;
  availabilityWarning?: string;
  updating?: boolean;
  rows: readonly AreaResultRowView[];
  emptyState?: AreaResultsEmptyState;
  pagination: {
    page: number;
    hasPrevious: boolean;
    hasNext: boolean;
    disabled: boolean;
  };
  onPage: (page: number) => void;
}
