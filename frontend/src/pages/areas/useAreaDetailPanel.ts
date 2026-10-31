import { useMutationDismissal } from '../../shared/components/useMutationDismissal';
import { useState } from 'react';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { useBuildingNames } from '../../shared/api/buildingNames';
import type { Notice } from '../../shared/components/Toast';
import type { EditTarget } from './PointTable';
import { pointEditorTitleId } from './pointRules';

export function useAreaDetailPanel(detail: AreaDetail) {
  const names = useBuildingNames();
  const [editing, setEditing] = useState<EditTarget | null>(null);
  const [areaOpen, setAreaOpen] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  // Always use this Area's current detail so stale point data cannot be edited after a refresh.
  const editedPoint = editing?.kind === 'point' ? detail.points.find((point) => point.id === editing.pointId) : undefined;
  const drawerPoint = editing?.kind === 'new-point' ? undefined : editedPoint;
  const drawerId = pointEditorTitleId(drawerPoint?.id);
  const drawerOpen = editing !== null && (editing.kind === 'new-point' || editedPoint !== undefined);
  const pointDismissal = useMutationDismissal(['createPoint', 'updatePoint', 'setPointActive', 'setSignRadius', 'setSignLocation', 'regenerateQr'], () => setEditing(null));
  const closeEditor = pointDismissal.close;
  const closeArea = () => setAreaOpen(false);
  const clearNotice = () => setNotice(null);
  const open = (target: EditTarget) => { setEditing(target); clearNotice(); };
  const openArea = () => { setAreaOpen(true); clearNotice(); };
  const saved = (text: string) => {
    // Successful saves close intentionally, independently of the user's pending-dismissal guard.
    setEditing(null); closeArea(); setNotice({ kind: 'ok', text });
  };
  return {
    names, area: detail.area, checkInSign: detail.checkInSign, areaOpen, notice, drawerPoint, drawerId,
    drawerOpen, closeEditor, closeArea, clearNotice, open, openArea, saved,
  };
}
