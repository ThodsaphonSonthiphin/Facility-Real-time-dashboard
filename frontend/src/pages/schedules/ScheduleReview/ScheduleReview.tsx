import type { ReactNode } from 'react';
import { Button } from '../../../shared/components/Button';
import { Drawer } from '../../../shared/components/Modal';
import { Note } from '../../../shared/components/Note';
import { ScheduleConflict } from '../ScheduleConflict/ScheduleConflict';
import type { ScheduleChangeLine, ScheduleReviewProps } from './type';

export function ScheduleReview({
  title, editor, children, latest, onCompare, onUseLatest, remaining, reviewLabel = 'ตรวจสอบ',
}: {
  title: string;
  editor: ScheduleReviewProps & { guard: { confirming: boolean; stay: () => void; discard: () => void } };
  children: ReactNode;
  remaining?: ReactNode;
  reviewLabel?: string;
  latest: ScheduleChangeLine[] | undefined;
  onCompare: () => void;
  onUseLatest: () => void;
}) {
  const { ui, phone, guard, busy } = editor;
  const size = phone ? 'phone' : 'desk';
  const main = phone ? 'main' : 'desk';
  return (
    <Drawer labelledBy="schedule-editor-heading" onClose={editor.cancel}>
      <div className={`flex min-w-0 flex-1 flex-col gap-4 ${phone ? 'text-base' : 'text-sm'}`}>
        <header className="flex flex-wrap items-start justify-between gap-2 border-b border-border pb-3">
          <h2 id="schedule-editor-heading" tabIndex={-1} className="text-xl font-bold">{title}</h2>
          <Button variant="link" size={size} onClick={editor.cancel} disabled={busy}>ปิด</Button>
        </header>
        {guard.confirming && (
          <Note tone="warn" title="มีข้อมูลที่ยังไม่ได้บันทึก">
            <div className="flex flex-wrap gap-2 pt-2">
              <Button variant="secondary" size={size} onClick={guard.stay}>อยู่หน้านี้</Button>
              <Button variant="danger" size={size} onClick={guard.discard} disabled={busy || ui === 'uncertain'}>
                ทิ้งร่าง
              </Button>
            </div>
          </Note>
        )}
        {(editor.changing || ui === 'conflict') && ui !== 'committed' && (
          <ScheduleConflict
            latest={latest}
            draft={editor.beforeAfter}
            size={size}
            onCompare={onCompare}
            onUseLatest={onUseLatest}
            disabled={busy || ui === 'uncertain'}
          />
        )}
        {editor.problems.length > 0 && ui !== 'committed' && (
          <Note tone="error" title="รายการที่ต้องตรวจสอบ">
            <ul className="list-disc pl-5">
              {editor.problems.map((problem) => (
                <li key={`${problem.rowId}:${problem.message}`}>
                  <ProblemLine problem={problem} lines={editor.beforeAfter} />
                </li>
              ))}
            </ul>
          </Note>
        )}
        {ui === 'editing' && children}
        {(ui === 'reviewing' || ui === 'sending') && (
          <section aria-label="ตรวจสอบก่อนบันทึก" className="space-y-3">
            <h3 className="text-lg font-semibold">ตรวจสอบก่อนบันทึก</h3>
            <ChangeList lines={editor.beforeAfter} />
            {remaining}
          </section>
        )}
        {ui === 'uncertain' && (
          <Note tone="warn" title="ยังยืนยันผลการบันทึกไม่ได้" status>
            ตรวจผลคำสั่งเดิมก่อนส่งซ้ำ
            {editor.confirmingUnresolvedExit && (
              <div className="mt-2 space-y-2">
                <p>หากออกตอนนี้ ระบบยังยืนยันไม่ได้ว่าคำสั่งสำเร็จหรือไม่ โปรดเก็บรหัสอ้างอิงและตรวจตารางก่อนทำรายการอีกครั้ง</p>
                {editor.unresolvedCommandId && <p className="break-all">รหัสอ้างอิง · {editor.unresolvedCommandId}</p>}
                <div className="flex flex-wrap gap-2">
                  <Button variant="secondary" size={size} onClick={editor.stayUnresolved}>อยู่หน้านี้</Button>
                  <Button variant="danger" size={size} disabled={busy} onClick={editor.confirmUnresolvedExit}>
                    ออกโดยยังไม่ทราบผล
                  </Button>
                </div>
              </div>
            )}
          </Note>
        )}
        {ui === 'committed' && (
          <>
            <Note tone="success" status>บันทึกการเปลี่ยนแปลงแล้ว</Note>
            {remaining}
          </>
        )}
        <footer className="mt-auto flex flex-wrap gap-2 border-t border-border pt-3">
          {ui === 'editing' && <Button variant="primary" size={main} disabled={!editor.canReview} onClick={editor.review}>{reviewLabel}</Button>}
          {ui === 'reviewing' && (
            <>
              <Button variant="secondary" size={size} onClick={editor.backToEdit}>กลับไปแก้ไข</Button>
              <Button variant="primary" size={main} disabled={!editor.canReview} onClick={editor.save}>บันทึก</Button>
            </>
          )}
          {ui === 'sending' && <p role="status">กำลังบันทึก...</p>}
          {ui === 'uncertain' && (
            <>
              <Button variant="primary" size={main} disabled={busy} onClick={editor.lookup}>
                {busy ? 'กำลังตรวจผล...' : 'ตรวจผลการบันทึก'}
              </Button>
              {editor.canRetry && <Button variant="secondary" size={size} onClick={editor.retry}>ส่งคำสั่งเดิมอีกครั้ง</Button>}
            </>
          )}
          {ui === 'committed' && <Button variant="primary" size={main} onClick={editor.cancel}>เสร็จสิ้น</Button>}
          {ui === 'editing' && <Button variant="secondary" size={size} onClick={editor.cancel}>ยกเลิก</Button>}
        </footer>
      </div>
    </Drawer>
  );
}

export function ChangeList({ lines }: { lines: ScheduleChangeLine[] }) {
  return (
    <ul className="space-y-2">
      {lines.map((line) => (
        <li key={line.id} className="space-y-1 rounded-lg border border-border bg-surface-2 p-3">
          <p className="font-semibold">{line.label}</p>
          <p className="break-words text-text-2">ก่อน · {line.before}</p>
          <p className="break-words">หลัง · {line.after}</p>
        </li>
      ))}
    </ul>
  );
}

function ProblemLine({ problem, lines }: {
  problem: ScheduleReviewProps['problems'][number];
  lines: ScheduleChangeLine[];
}) {
  const label = lines.find((line) => line.id === problem.rowId)?.label;
  return <>{label && <strong>{label} · </strong>}{problem.message}</>;
}
