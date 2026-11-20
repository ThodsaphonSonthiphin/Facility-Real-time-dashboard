import type { Shift } from '../../../shared/api/types';
import type {
  OverviewAreaList,
  OverviewGroup,
  OverviewSelection,
  OverviewView,
} from '../../../shared/overview/types';
import type { AreaResultsProps } from '../AreaResults/type';
import type { BuildingComparisonProps } from '../BuildingComparison/type';
import type { CoveragePanelProps } from '../CoveragePanel/type';
import type { FollowUpGroupsProps } from '../FollowUpGroups/type';
import type { RoundHistoryProps } from '../RoundHistory/type';

export interface BuildingOption {
  id: number;
  label: string;
}

export interface OverviewPageModel {
  selection: OverviewSelection;
  response?: OverviewAreaList;
  loading: boolean;
  refreshing: boolean;
  recovering: boolean;
  hubKey?: string;
  error?: string;
  changed: boolean;
  onDate: (date: string) => void;
  onShift: (shift: Shift) => void;
  onBuilding: (id?: number) => void;
  onGroup: (group: OverviewGroup) => void;
  onPage: (page: number) => void;
  retry: () => void;
  href: (areaId: number, view: OverviewView, roundKey?: string) => string;

  invalidContext: boolean;
  periodText: string;
  readAtText?: string;
  updatedNotice?: string;
  notStartedMessage?: string;
  dateInputValue: string;
  dateMaxValue: string;
  dateHelpText?: string;
  shiftValue?: Shift;
  shiftOptions: readonly { value: Shift; label: string }[];
  buildingValue: string;
  buildingOptions: readonly BuildingOption[];
  controlsDisabled: boolean;
  canResetToCurrent: boolean;
  onResetToCurrent: () => void;
  onBuildingGroup: (buildingId: number, group: OverviewGroup) => void;
  panels: readonly CoveragePanelProps[];
  followUp: FollowUpGroupsProps;
  buildings: BuildingComparisonProps;
  history: RoundHistoryProps;
  results: AreaResultsProps;
  statusAnnouncement: string;
}
