import { Button } from '../../../shared/components/Button';
import { Note } from '../../../shared/components/Note';
import { PageLoading } from '../../../shared/components/PageState';
import { Toast } from '../../../shared/components/Toast';
import { CorrectionForm } from '../CorrectionForm';
import { CoverDrawer } from '../CoverDrawer';
import type { OverviewAttendanceActionsProps } from './type';
import { useOverviewAttendanceActions, useScopedAttendanceActionArgs } from './useOverviewAttendanceActions';

export function ScopedAttendanceActions() {
  const scope = useScopedAttendanceActionArgs();
  return scope && (
    <OverviewAttendanceActions key={`${scope.shiftDate}:${scope.shift}:${scope.areaId}`} {...scope} />
  );
}

export function OverviewAttendanceActions(scope: OverviewAttendanceActionsProps) {
  const model = useOverviewAttendanceActions(scope);
  const { action, board, row } = model;
  return (
    <section className="flex flex-col gap-2" aria-labelledby="overview-attendance-actions">
      <h2 id="overview-attendance-actions" className="text-lg font-semibold">จัดการการเข้างานกะปัจจุบัน</h2>
      {model.notice && <Toast notice={model.notice} onClose={model.clearNotice} />}
      {model.isFetching && !board && <PageLoading>กำลังตรวจข้อมูลกะปัจจุบัน...</PageLoading>}
      {model.error && <Note tone="error" onRetry={model.retry}>{model.error}</Note>}
      {!model.isFetching && !model.error && !row && (
        <Note tone="info">ไม่พบพื้นที่นี้ในกะปัจจุบัน กรุณาตรวจข้อมูลอีกครั้ง</Note>
      )}
      {row && !model.error && (
        <div className="flex flex-wrap gap-2">
          {row.cleaner && (
            <Button size="desk" disabled={model.opening || !!action} onClick={() => void model.openAction('correction')}>
              แก้เวลา {row.cleaner.displayName}
            </Button>
          )}
          <Button size="desk" disabled={model.opening || !!action} onClick={() => void model.openAction('cover')}>
            มอบหมายทำแทน
          </Button>
        </div>
      )}
      {action?.kind === 'correction' && board && row?.cleaner && (
        <CorrectionForm
          key={`${board.shiftDate}:${board.shift}:${row.areaId}:${row.cleaner.id}:${action.id}`}
          board={board} row={row} cleaner={row.cleaner}
          beforeWrite={() => model.canSubmit(action)}
          onDone={(text) => model.done(action, text)}
          onCancel={() => model.close(action)}
        />
      )}
      {action?.kind === 'cover' && row && board && (
        <CoverDrawer
          key={action.id}
          current={scope} start={scope}
          scoped={{ areaId: scope.areaId, board, beforeWrite: () => model.canSubmit(action) }}
          onDone={(text) => model.done(action, text)}
          onCancel={() => model.close(action)}
        />
      )}
    </section>
  );
}
