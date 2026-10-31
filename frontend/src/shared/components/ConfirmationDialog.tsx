import { Power, QrCode, MapPin, Building2, ArrowRightLeft } from 'lucide-react';
import { Button } from './Button';
import { Field, TextInput } from './Field';
import { Modal } from './Modal';
import { Note } from './Note';
import { useConfirmationDialog } from './useConfirmationDialog';

interface ConfirmationDialogProps {
  title: string;
  body: string;
  impacts?: string[];
  confirmLabel: string;
  kind?: 'account' | 'qr' | 'area' | 'building' | 'cover' | 'point';
  code?: string;
  printFollowup?: boolean;
  onConfirm: (print: boolean) => Promise<void>;
  onCancel: () => void;
}

const icons = {
  account: Power, qr: QrCode, area: MapPin, building: Building2, cover: ArrowRightLeft, point: MapPin,
};

export function ConfirmationDialog({
  title, body, impacts = [], confirmLabel, kind = 'account', code, printFollowup, onConfirm, onCancel,
}: ConfirmationDialogProps) {
  const state = useConfirmationDialog(onConfirm, onCancel, code);
  const Icon = icons[kind];
  const destructive = kind !== 'cover';
  const tone = kind === 'qr'
    ? 'bg-overdue-bg text-overdue'
    : destructive ? 'bg-rework-bg text-rework' : 'bg-accent-subtle text-accent';
  return (
    <Modal labelledBy={state.id} describedBy={`${state.id}-body`} role="alertdialog" onClose={state.cancel}>
      <div className="flex flex-col gap-5 text-sm">
        <div className="flex items-start gap-4">
          <span className={`flex size-10 shrink-0 items-center justify-center rounded-full ${tone}`}>
            <Icon aria-hidden="true" className="size-5" />
          </span>
          <div className="flex min-w-0 flex-1 flex-col gap-2">
            <h2 id={state.id} tabIndex={-1} className="text-lg font-bold">{title}</h2>
            <p id={`${state.id}-body`}>{body}</p>
            {impacts.length > 0 && (
              <ul className="list-disc space-y-0.5 pl-5 text-text-3">
                {impacts.map((impact) => <li key={impact}>{impact}</li>)}
              </ul>
            )}
            {code !== undefined && (
              <Field label={<>พิมพ์ <strong className="font-mono">{code}</strong> เพื่อยืนยัน</>}>
                <TextInput
                  aria-label="รหัสยืนยัน"
                  size="desk"
                  className="w-full font-mono"
                  value={state.typed}
                  disabled={state.pending}
                  onChange={(event) => state.setTyped(event.target.value)}
                  // This dialog may be inside another form: Enter must not submit its ancestor.
                  onKeyDown={(event) => { if (event.key === 'Enter') event.preventDefault(); }}
                />
              </Field>
            )}
            {printFollowup && (
              <label className="flex min-h-9 items-center gap-2">
                <input
                  type="checkbox"
                  className="size-4 accent-accent focus-visible:outline-2 focus-visible:outline-accent"
                  checked={state.print}
                  disabled={state.pending}
                  onChange={(event) => state.setPrint(event.target.checked)}
                />
                ไปหน้าพิมพ์ป้ายต่อเลย
              </label>
            )}
          </div>
        </div>
        {state.problem && <Note tone="error">{state.problem}</Note>}
        <div className="flex flex-wrap justify-end gap-2">
          <Button size="desk" disabled={state.pending} onClick={state.cancel}>ยกเลิก</Button>
          {destructive ? (
            <button
              type="button"
              data-tap=""
              className="inline-flex min-h-9 items-center justify-center rounded-md border border-transparent bg-rework px-4 text-sm font-semibold text-on-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:bg-border disabled:text-text-2 not-disabled:hover:bg-rework-strong"
              disabled={state.disabled}
              onClick={() => void state.confirm()}
            >
              {state.pending ? 'กำลังดำเนินการ...' : confirmLabel}
            </button>
          ) : (
            <Button variant="primary" size="desk" disabled={state.disabled} onClick={() => void state.confirm()}>
              {state.pending ? 'กำลังดำเนินการ...' : confirmLabel}
            </Button>
          )}
        </div>
      </div>
    </Modal>
  );
}
