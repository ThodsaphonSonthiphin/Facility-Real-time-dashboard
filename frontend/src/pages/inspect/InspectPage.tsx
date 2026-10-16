import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/myWorkApi';
import { statusOf } from '../../shared/api/scanAnswers';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { InspectForm } from './InspectForm';
import { InspectResult } from './InspectResult';
import { NothingToInspect } from './NothingToInspect';
import { useOpenInspectionQuery, useRecordInspectionMutation, type InspectionRecorded, type InspectionVisit } from './inspectApi';
import { emptyDraft, requestOf, type InspectDraft } from './inspectRules';
import './inspect.css';

type Outcome =
  | { kind: 'recorded'; result: InspectionRecorded; visit: InspectionVisit }
  | { kind: 'refused'; message: string }
  | { kind: 'changed' };

/** Plan 9 decision 5: the work changed between the scan and the press, so the page opened the newest state instead. */
const CHANGED = 'งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว';

/**
 * Wireframes M6, M7 and M16: the page a point's QR Sign opens for a Supervisor (/scan/<token>, facility-0029). Opening
 * scans the sign; บันทึกผลตรวจ records the verdict. The server decides what may be inspected, the flag and the block
 * (facility-0037, 0047 rule 7, 0048).
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
        setOutcome({ kind: 'refused', message: 'อ่านตำแหน่งไม่ได้ ลองอีกครั้ง' });
        return;
      }
      const answer = await record(requestOf(token, submission.id, draft, reading.fix));
      if (answer.data) {
        setOutcome({ kind: 'recorded', result: answer.data, visit });
        return;
      }
      if (statusOf(answer.error) === 409) {
        // Never judge work the Supervisor was not shown: open the newest state instead
        setDraft(emptyDraft);
        setOutcome({ kind: 'changed' });
        void visitQuery.refetch();
        return;
      }
      setOutcome({ kind: 'refused', message: getErrorMessage(answer.error) });
    } finally {
      setSending(false);
    }
  };

  if (outcome?.kind === 'recorded') {
    return <InspectResult visit={outcome.visit} draft={draft} result={outcome.result} />;
  }
  // Decide only on a fresh answer: the cache may hold this sign's scan from an earlier visit
  if (visitQuery.isFetching) {
    return <p className="app-splash">กำลังอ่านตำแหน่งและเปิดงานตรวจ...</p>;
  }
  if (visitQuery.error) {
    return (
      <StatusScreen
        title={getErrorMessage(visitQuery.error)}
        detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
        linkTo="/my-building"
        linkText="กลับไปตึกของฉัน"
      />
    );
  }
  if (!visit) {
    return <p className="app-splash">กำลังอ่านตำแหน่งและเปิดงานตรวจ...</p>;
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
