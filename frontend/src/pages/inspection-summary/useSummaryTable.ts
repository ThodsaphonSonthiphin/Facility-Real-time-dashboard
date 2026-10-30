import { useId, useState } from 'react';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { inspectedShare, placeText } from './summaryRules';
import type { SummaryTableProps } from './type';

export function useSummaryTable({ rows }: SummaryTableProps) {
  const names = useBuildingNames();
  const prefix = useId();
  const [expanded, setExpanded] = useState<number | null>(null);
  return {
    rows: rows.map((row) => ({
      row,
      place: placeText(row, names),
      missed: row.pointsTotal - row.pointsInspected,
      share: inspectedShare(row),
      id: `${prefix}-supervisor-${row.supervisorId}`,
      expanded: expanded === row.supervisorId,
    })),
    toggle: (id: number) => setExpanded((current) => current === id ? null : id),
  };
}
