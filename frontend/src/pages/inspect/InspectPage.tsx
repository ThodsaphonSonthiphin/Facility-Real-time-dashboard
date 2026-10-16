import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../shared/api/scanAnswers';
import type { BlockedScan } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { InspectForm } from './InspectForm';
import { InspectResult } from './InspectResult';
import { InspectSendFailed } from './InspectSendFailed';
import { NothingToInspect } from './NothingToInspect';
import { useOpenInspectionQuery, useRecordInspectionMutation, type InspectionRecorded, type InspectionVisit } from './inspectApi';
import { emptyDraft, locationProblemOf, requestOf, type InspectDraft } from './inspectRules';
import './inspect.css';

type Outcome =
  | { kind: 'recorded'; result: InspectionRecorded; visit: InspectionVisit }
  | { kind: 'refused'; message: string }
  | { kind: 'changed' }
  | { kind: 'location'; problem: LocationProblem }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'not-sent'; triedAt: Date };

/** Plan 9 decision 5: the work changed between the scan and the press, so the page opened the newest state instead. */
const CHANGED = 'งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว';

/**
 * Wireframes M2, M6, M7, M8, M12 and M16: the page a point's QR Sign opens for a Supervisor (/scan/<token>,
 * facility-0029). Opening scans the sign; บันทึกผลตรวจ records the verdict. The server decides what may be inspected,
 * the flag and the block (facility-0037, 0047 rule 7, 0048). A press that does not go through keeps the verdict for
 * ส่งใหม่ (facility-0052).
 */
export function InspectPage() {
  const { token = '' } = useParams();
  const visitQuery = useOpenInspectionQuery(token, freshOnOpen);
  const [record] = useRecordInspectionMutation();
  const [draft, setDraft] = useState<InspectDraft>(emptyDraft);
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const visit = visitQuery.data;

  const send = async () => {
    const submission = visit?.submission;
    if (!visit || !submission) {
      return;
    }
    setSending(true);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind });
        return;
      }
      const triedAt = new Date();
      const answer = await record(requestOf(token, submission.id, draft, reading.fix));
      if (statusOf(answer.error) === 409) {
        // Never judge work the Supervisor was not shown: open the newest state instead
        setDraft(emptyDraft);
        setOutcome({ kind: 'changed' });
        void visitQuery.refetch();
        return;
      }
      setOutcome(outcomeOf(answer, visit, triedAt));
    } finally {
      setSending(false);
    }
  };

  if (outcome?.kind === 'recorded') {
    return <InspectResult visit={outcome.visit} draft={draft} result={outcome.result} />;
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="inspect" />;
  }
  if (outcome?.kind === 'location') {
    return <LocationHelp problem={outcome.problem} action="บันทึกผลตรวจ" busy={sending} onRetry={() => void send()} />;
  }
  if (outcome?.kind === 'not-sent' && visit) {
    return (
      <InspectSendFailed visit={visit} draft={draft} triedAt={outcome.triedAt} sending={sending} onResend={() => void send()} />
    );
  }
  // Decide only on a fresh answer: the cache may hold this sign's scan from an earlier visit
  if (visitQuery.isFetching || (!visit && !visitQuery.error)) {
    return <p className="app-splash">กำลังอ่านตำแหน่งและเปิดงานตรวจ...</p>;
  }
  if (visitQuery.error || !visit) {
    return <OpenFailed error={visitQuery.error} onRetry={() => void visitQuery.refetch()} />;
  }
  const notice = outcome?.kind === 'changed' ? CHANGED : null;
  if (!visit.submission) {
    return <NothingToInspect visit={visit} notice={notice} />;
  }
  return (
    <InspectForm
      visit={visit}
      submission={visit.submission}
      draft={draft}
      onChange={setDraft}
      sending={sending}
      notice={notice}
      errorText={outcome?.kind === 'refused' ? outcome.message : null}
      onSend={() => void send()}
    />
  );
}

/** Why the sign did not open: Location (M2), a Blocked Scan the server just recorded (M8), no answer, or a refusal. */
function OpenFailed({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const problem = locationProblemOf(error);
  if (problem) {
    return <LocationHelp problem={problem} action="ตรวจงาน" busy={false} onRetry={onRetry} />;
  }
  const blocked = blockedScanOf(error);
  if (blocked) {
    return <BlockedScanScreen blocked={blocked} sign="inspect" />;
  }
  if (isNotSent(error)) {
    return (
      <main className="app-help">
        <h1 className="app-help__title">เปิดงานตรวจไม่ได้</h1>
        <p className="app-note app-note--error">เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ยังไม่ได้ตรวจงาน เดินไปตรงที่มีสัญญาณ แล้วกดลองอีกครั้ง</p>
        <button type="button" className="app-button app-button--primary" onClick={onRetry}>
          ลองอีกครั้ง
        </button>
      </main>
    );
  }
  return (
    <StatusScreen
      title={getErrorMessage(error)}
      detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
      linkTo="/my-building"
      linkText="กลับไปตึกของฉัน"
    />
  );
}

function outcomeOf(answer: { data?: InspectionRecorded; error?: unknown }, visit: InspectionVisit, triedAt: Date): Outcome {
  if (answer.data) {
    return { kind: 'recorded', result: answer.data, visit };
  }
  const blocked = blockedScanOf(answer.error);
  if (blocked) {
    return { kind: 'blocked', blocked };
  }
  if (isNotSent(answer.error)) {
    return { kind: 'not-sent', triedAt };
  }
  return { kind: 'refused', message: getErrorMessage(answer.error) };
}
