import { useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent } from '../../shared/api/scanAnswers';
import type { BlockedScan } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { OffDuty } from './OffDuty';
import { SendFailed } from './SendFailed';
import { SubmitForm } from './SubmitForm';
import { SubmitResult } from './SubmitResult';
import { useGetPointByTokenQuery, useSubmitScanMutation, type ScanRecordCreated } from './scanApi';
import { emptyDraft, offDutyReason, requestOf, type ScanDraft } from './scanRules';
import './scan.css';

type Outcome =
  | { kind: 'saved'; result: ScanRecordCreated }
  | { kind: 'refused'; message: string }
  | { kind: 'location'; problem: LocationProblem }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'not-sent'; triedAt: Date };

/**
 * Wireframes M2, M4, M5, M8, M11, M12 and M13: the page a point's QR Sign opens (/scan/<token>, facility-0026). The
 * Cleaner presses ส่งงาน; the server decides the round, the flag and whether the Area is theirs (facility-0037,
 * 0041, 0047). A send that does not go through keeps the choices for ส่งใหม่ (facility-0052).
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
      const triedAt = new Date();
      setOutcome(outcomeOf(await submit(requestOf(token, draft, reading.fix)), triedAt));
    } finally {
      setSending(false);
    }
  };

  if (pointQuery.error) {
    return (
      <StatusScreen
        title={getErrorMessage(pointQuery.error)}
        detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้งแอดมิน"
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
  if (outcome?.kind === 'blocked') {
    const ownArea = work.areas.find((area) => !area.isCover);
    return <BlockedScanScreen blocked={outcome.blocked} sign="point" ownAreaName={ownArea?.areaName} />;
  }
  if (outcome?.kind === 'location') {
    return <LocationHelp problem={outcome.problem} action="ส่งงาน" busy={sending} onRetry={() => void send()} />;
  }
  if (outcome?.kind === 'not-sent') {
    return (
      <SendFailed point={point} draft={draft} triedAt={outcome.triedAt} sending={sending} onResend={() => void send()} />
    );
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

function outcomeOf(answer: { data?: ScanRecordCreated; error?: unknown }, triedAt: Date): Outcome {
  if (answer.data) {
    return { kind: 'saved', result: answer.data };
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
