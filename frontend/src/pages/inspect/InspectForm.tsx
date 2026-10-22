import { CircleCheck, CircleX, ClipboardCheck } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Panel } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { ChoiceTile, Field, TextArea } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
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
    <main className="flex flex-col gap-4 text-base text-text">
      <InspectHeading visit={visit} />
      {notice && (
        <Note tone="warn" status>
          {notice}
        </Note>
      )}
      <Panel title="ส่งงานล่าสุด">
        <p className="font-semibold">{submissionLine(submission, visit.point)}</p>
      </Panel>
      <DistanceLine visit={visit} />
      <form className="flex flex-col gap-4" onSubmit={onSubmit}>
        {/* The two verdicts as large tiles (#76), still a real radio group */}
        <fieldset className="m-0 border-0 p-0">
          <legend className="mb-2 p-0 font-bold">ผลการตรวจ</legend>
          <div className="grid grid-cols-2 gap-2.5">
            <ChoiceTile
              name="inspect-result"
              disabled={sending}
              checked={draft.result === 'Passed'}
              onChange={() => onChange({ ...draft, result: 'Passed' })}
            >
              <CircleCheck aria-hidden="true" className="size-7" />
              สะอาด
            </ChoiceTile>
            <ChoiceTile
              name="inspect-result"
              disabled={sending}
              checked={draft.result === 'Rework'}
              onChange={() => onChange({ ...draft, result: 'Rework' })}
            >
              <CircleX aria-hidden="true" className="size-7" />
              ไม่สะอาด
            </ChoiceTile>
          </div>
        </fieldset>
        {draft.result === 'Rework' && (
          <Field label="สิ่งที่ต้องแก้">
            <TextArea
              rows={4}
              maxLength={MAX_DEFECT_LENGTH}
              disabled={sending}
              value={draft.defect}
              onChange={(event) => onChange({ ...draft, defect: event.target.value })}
            />
          </Field>
        )}
        {alert && <Note tone="error">{alert}</Note>}
        <Button type="submit" variant="primary" size="main" disabled={sending}>
          <ClipboardCheck aria-hidden="true" className="size-5" />
          {sending ? 'กำลังบันทึก...' : 'บันทึกผลตรวจ'}
        </Button>
      </form>
      <p className="text-center text-md text-muted">ไม่สะอาด = แม่บ้านเห็นสิ่งที่ต้องแก้ทันที แก้แล้วส่งงานใหม่ · ตรวจกี่ครั้งก็ได้</p>
    </main>
  );
}
