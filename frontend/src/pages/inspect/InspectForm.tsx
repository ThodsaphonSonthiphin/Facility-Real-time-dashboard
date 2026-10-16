import { useState, type FormEvent } from 'react';
import { DistanceLine } from './DistanceLine';
import { InspectHeading } from './InspectHeading';
import type { InspectionSubmission, InspectionVisit } from './inspectApi';
import { draftProblem, MAX_DEFECT_LENGTH, submissionLine, type InspectDraft } from './inspectRules';

interface InspectFormProps {
  visit: InspectionVisit;
  submission: InspectionSubmission;
  draft: InspectDraft;
  onChange: (draft: InspectDraft) => void;
  sending: boolean;
  /** Why the page opened again (a 409, decision 5). */
  notice: string | null;
  /** The server's refusal of the last press, shown as it is (facility-0022 item 4). */
  errorText: string | null;
  onSend: () => void;
}

/** Wireframe M6 (facility-0029): the latest submission of the current round, and the verdict. */
export function InspectForm({ visit, submission, draft, onChange, sending, notice, errorText, onSend }: InspectFormProps) {
  const [problem, setProblem] = useState<string | null>(null);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(draft);
    setProblem(found);
    if (!found) {
      onSend();
    }
  };

  const alert = problem ?? errorText;
  return (
    <main className="inspect-page">
      <InspectHeading visit={visit} />
      {notice && (
        <p className="app-note app-note--warn" role="status">
          {notice}
        </p>
      )}
      <section className="app-panel">
        <h2>ส่งงานล่าสุด</h2>
        <p className="inspect-strong">{submissionLine(submission, visit.point)}</p>
      </section>
      <DistanceLine visit={visit} />
      <form className="inspect-form" onSubmit={onSubmit}>
        <fieldset className="inspect-choice">
          <legend>ผลการตรวจ</legend>
          <label>
            <input
              type="radio"
              name="inspect-result"
              disabled={sending}
              checked={draft.result === 'Passed'}
              onChange={() => onChange({ ...draft, result: 'Passed' })}
            />{' '}
            สะอาด
          </label>
          <label className="inspect-choice__rework">
            <input
              type="radio"
              name="inspect-result"
              disabled={sending}
              checked={draft.result === 'Rework'}
              onChange={() => onChange({ ...draft, result: 'Rework' })}
            />{' '}
            ไม่สะอาด
          </label>
        </fieldset>
        {draft.result === 'Rework' && (
          <div className="inspect-defect">
            <label htmlFor="inspect-defect">ข้อบกพร่อง</label>
            <textarea
              id="inspect-defect"
              rows={4}
              maxLength={MAX_DEFECT_LENGTH}
              disabled={sending}
              value={draft.defect}
              onChange={(event) => onChange({ ...draft, defect: event.target.value })}
            />
          </div>
        )}
        {alert && (
          <p className="app-note app-note--error" role="alert">
            {alert}
          </p>
        )}
        <button type="submit" className="inspect-submit" disabled={sending}>
          {sending ? 'กำลังบันทึก...' : 'บันทึกผลตรวจ'}
        </button>
      </form>
      <p className="inspect-muted">ไม่สะอาด = แม่บ้านเห็นข้อบกพร่องทันที แก้แล้วส่งงานใหม่ · ตรวจกี่ครั้งก็ได้</p>
    </main>
  );
}
