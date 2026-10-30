import type { SummaryRow } from './summaryApi';

export interface SummaryTableProps {
  rows: SummaryRow[];
  filteredShift?: boolean;
}

export interface SummaryRowDetailsProps {
  row: SummaryRow;
  id: string;
  expanded: boolean;
  place: string;
  share: number | null;
}
