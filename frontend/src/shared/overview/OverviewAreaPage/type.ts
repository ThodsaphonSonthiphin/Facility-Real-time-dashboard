import type { ReactNode } from 'react';
import type { OverviewAreaDetail, OverviewSelection, OverviewView } from '../types';

export interface OverviewAreaPageProps {
  view: OverviewView;
  actions?: ReactNode;
}

export interface OverviewAreaPageModel {
  detail?: OverviewAreaDetail;
  loading: boolean;
  error?: string;
  backHref: string;
  readOnly: boolean;
  stateChanged: boolean;
  partial: boolean;
  retry(): void;
  canRetry: boolean;
  periodText: string;
  phaseText?: string;
  view: OverviewView;
  selection?: OverviewSelection;
}
