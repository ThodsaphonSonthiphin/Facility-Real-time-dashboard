import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { OffDuty } from './OffDuty';
import { SubmitForm } from './SubmitForm';
import { SubmitResult } from './SubmitResult';
import { useGetPointByTokenQuery, useSubmitScanMutation, type ScanRecordCreated } from './scanApi';
import { emptyDraft, offDutyReason, requestOf, type ScanDraft } from './scanRules';
import './scan.css';

type Outcome =
  | { kind: 'saved'; result: ScanRecordCreated }
  | { kind: 'refused'; message: string }
  | { kind: 'location'; problem: LocationProblem };

/**
 * Wireframes M2, M4, M5, M11 and M13: the page a point's QR Sign opens (/scan/<token>, facility-0026). The Cleaner
 * presses ส่งงาน; the server decides the round, the flag and whether the Area is theirs (facility-0037, 0041, 0047).
 */
export function ScanPage() {
  const { token = '' } = useParams();
  const pointQuery = useGetPointByTokenQuery(token, freshOnOpen);
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [submit] = useSubmitScanMutation();
  const [draft, setDraft] = useState<ScanDraft>(emptyDraft);
  const [sending, setSending] = useState(false);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const send = async () => {
    setSending(true);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind });
        return;
      }
      setOutcome(outcomeOf(await submit(requestOf(token, draft, reading.fix))));
    } finally {
      setSending(false);
    }
  };

  if (pointQuery.error) {
    return (
      <StatusScreen
        title={getErrorMessage(pointQuery.error)}
        detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"
        linkTo="/my-work"
        linkText="ดูงานของฉัน"
      />
    );
  }
  if (workQuery.error && !workQuery.data) {
    return <StatusScreen title="เปิดหน้านี้ไม่ได้" detail={getErrorMessage(workQuery.error)} linkTo="/my-work" linkText="ดูงานของฉัน" />;
  }
  const point = pointQuery.data;
  const work = workQuery.data;
  // Decide only on fresh answers: the cache may hold My Work from before the Cleaner checked in
  if (!point || !work || pointQuery.isFetching || workQuery.isFetching) {
    return <p className="app-splash">กำลังเปิดป้าย...</p>;
  }

  if (outcome?.kind === 'saved') {
    return <SubmitResult point={point} draft={draft} result={outcome.result} />;
  }
  if (outcome?.kind === 'location') {
    return <LocationHelp problem={outcome.problem} action="ส่งงาน" busy={sending} onRetry={() => void send()} />;
  }
  const offDuty = offDutyReason(work.attendance);
  if (offDuty) {
    return <OffDuty reason={offDuty} point={point} />;
  }
  return (
    <SubmitForm
      point={point}
      draft={draft}
      onChange={setDraft}
      sending={sending}
      errorText={outcome?.kind === 'refused' ? outcome.message : null}
      onSend={() => void send()}
    />
  );
}

function outcomeOf(answer: { data?: ScanRecordCreated; error?: unknown }): Outcome {
  if (answer.data) {
    return { kind: 'saved', result: answer.data };
  }
  return { kind: 'refused', message: getErrorMessage(answer.error) };
}
