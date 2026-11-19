import type { OverviewAreaDetail, OverviewSelection } from '../types';

export interface AreaWorkProps {
  detail: OverviewAreaDetail;
  selection: OverviewSelection;
  view: 'cleaning' | 'inspection';
}
