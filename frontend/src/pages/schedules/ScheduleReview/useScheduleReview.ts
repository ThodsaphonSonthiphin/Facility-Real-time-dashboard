import { useEffect, useRef, useState } from 'react';
import { useBeforeUnload, useBlocker } from 'react-router';
import type { CommandResult, RowProblem, ScheduleCommandEnvelope } from '../../../shared/api/scheduleTypes';
import { resolveCommandUi, resultFromError, type CommandDraft, type CommandUi } from '../commandRules';
import {
  useExecuteScheduleCommandMutation,
  useIssueScheduleCommandTokenMutation,
  useLazyGetScheduleCommandResultQuery,
} from '../schedulesApi';

export function useScheduleReview({
  command, beforeAfter, problems: validation, dirty, ready = true, phone, changing, onClose, verifyRetryScope,
}: {
  command: CommandDraft;
  beforeAfter: import('./type').ScheduleChangeLine[];
  problems: RowProblem[];
  dirty: boolean;
  ready?: boolean;
  phone: boolean;
  changing: boolean;
  onClose: () => void;
  verifyRetryScope?: () => Promise<boolean>;
}) {
  const [ui, setUi] = useState<CommandUi>('editing');
  const [problems, setProblems] = useState<RowProblem[]>([]);
  const [retryOffered, setCanRetry] = useState(false);
  const [confirmingUnresolvedExit, setConfirmingUnresolvedExit] = useState(false);
  const [identity, setIdentity] = useState<{ commandId: string; expiresAt: string }>();
  const [busy, setBusy] = useState(false);
  const running = useRef(false);
  const pending = useRef<{ envelope: ScheduleCommandEnvelope; expiresAt: string } | null>(null);
  const [issueToken] = useIssueScheduleCommandTokenMutation();
  const [execute] = useExecuteScheduleCommandMutation();
  const [getResult] = useLazyGetScheduleCommandResultQuery();
  const guard = useScheduleNavigation((dirty && ui !== 'committed') || busy);
  const canReview = ready && dirty && validation.length === 0 && !changing;
  const expiry = identity ? Date.parse(identity.expiresAt) : 0;
  const canRetry = retryOffered && !changing;

  useEffect(() => {
    if (!retryOffered) return;
    const timer = window.setTimeout(() => setCanRetry(false), Math.max(0, expiry - Date.now()));
    return () => window.clearTimeout(timer);
  }, [expiry, retryOffered]);

  function apply(result: CommandResult) {
    if (result.outcome === 'committed') {
      setUi((state) => resolveCommandUi(state, 'committed'));
      return;
    }
    const stale = result.problems.some((problem) => problem.code === 'stale-revision');
    if (ui === 'uncertain' && result.outcome !== 'rejected') {
      setUi('uncertain');
      return;
    }
    setProblems(result.problems.length ? result.problems : [{
      rowId: 'command', code: 'row-invalid', message: commandRefusal(result.outcome),
    }]);
    setUi(stale ? resolveCommandUi('sending', 'stale') : 'editing');
    pending.current = null;
  }

  async function send(envelope: ScheduleCommandEnvelope) {
    try {
      apply(await execute(envelope).unwrap());
    } catch (error) {
      const result = resultFromError(error, envelope.commandId);
      if (result) apply(result);
      else setUi(resolveCommandUi('sending', 'network-error'));
    }
  }

  async function save() {
    if (ui !== 'reviewing' || !canReview || running.current) return;
    running.current = true;
    setBusy(true);
    setUi('sending');
    // Freeze the exact reviewed body before the first network step. The shared 401 refresh also retries this body.
    const reviewed = structuredClone(command);
    setIdentity(undefined);
    try {
      const issued = await issueToken().unwrap();
      const envelope = { ...reviewed, token: issued.token, commandId: issued.commandId };
      pending.current = { envelope, expiresAt: issued.expiresAt };
      setIdentity({ commandId: issued.commandId, expiresAt: issued.expiresAt });
      await send(envelope);
    } catch {
      setProblems([{ rowId: 'command', code: 'row-invalid', message: 'ขอรหัสสำหรับบันทึกไม่สำเร็จ กรุณาลองอีกครั้ง' }]);
      setUi('reviewing');
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  async function lookup() {
    const attempt = pending.current;
    if (!attempt || ui !== 'uncertain' || running.current) return;
    running.current = true;
    setBusy(true);
    setCanRetry(false);
    try {
      const result = await getResult(attempt.envelope.commandId, false).unwrap();
      if (result.outcome === 'committed' && result.commandId === attempt.envelope.commandId) apply(result);
    } catch (error) {
      setUi(resolveCommandUi('uncertain', 'lookup-failed'));
      // The lookup uses 404 for both a missing receipt and one outside the caller's current scope.
      const missing = typeof error === 'object' && error !== null && 'status' in error && error.status === 404;
      if (missing && verifyRetryScope && Date.parse(attempt.expiresAt) > Date.now()) {
        const authorised = await verifyRetryScope().catch(() => false);
        setCanRetry(authorised && Date.parse(attempt.expiresAt) > Date.now());
      }
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  async function retry() {
    const attempt = pending.current;
    if (!attempt || !canRetry || ui !== 'uncertain' || running.current) return;
    setCanRetry(false);
    if (Date.parse(attempt.expiresAt) <= Date.now()) return;
    running.current = true;
    setBusy(true);
    try {
      await send(attempt.envelope);
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  return {
    ui, beforeAfter, problems: [...validation, ...problems], canReview, dirty, phone, busy, canRetry, changing,
    unresolvedCommandId: ui === 'uncertain' ? identity?.commandId : undefined,
    confirmingUnresolvedExit,
    confirmUnresolvedExit: () => {
      if (ui === 'uncertain' && !running.current) onClose();
    },
    stayUnresolved: () => setConfirmingUnresolvedExit(false),
    guard,
    review: () => {
      if (!canReview) return;
      setProblems([]);
      setUi('reviewing');
    },
    backToEdit: () => setUi('editing'),
    cancel: () => {
      if (running.current) return;
      if (ui === 'uncertain') setConfirmingUnresolvedExit(true);
      else guard.leave(onClose, ui !== 'committed');
    },
    save, lookup, retry,
  };
}

function commandRefusal(outcome: CommandResult['outcome']) {
  switch (outcome) {
    case 'command-expired': return 'รหัสบันทึกหมดอายุ กรุณาตรวจข้อมูลก่อนบันทึกใหม่';
    case 'command-not-authorised': return 'บัญชีนี้ไม่มีสิทธิ์บันทึกรายการนี้';
    case 'command-payload-mismatch': return 'ข้อมูลไม่ตรงกับรหัสบันทึก กรุณาตรวจผลคำสั่ง';
    default: return 'บันทึกรายการไม่สำเร็จ กรุณาตรวจข้อมูล';
  }
}

export function useScheduleNavigation(dirty: boolean) {
  const blocker = useBlocker(dirty);
  const [discard, setDiscard] = useState<{ leave: () => void } | null>(null);
  useBeforeUnload((event) => {
    if (!dirty) return;
    event.preventDefault();
    event.returnValue = '';
  });

  useEffect(() => {
    if (blocker.state === 'blocked' && !dirty) blocker.proceed();
  }, [blocker, dirty]);

  return {
    confirming: !!discard || blocker.state === 'blocked',
    stay: () => {
      setDiscard(null);
      if (blocker.state === 'blocked') blocker.reset();
    },
    discard: () => {
      if (blocker.state === 'blocked') blocker.proceed();
      discard?.leave();
    },
    leave: (leave: () => void, protect: boolean) => {
      if (protect && dirty) setDiscard({ leave });
      else leave();
    },
  };
}
