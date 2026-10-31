import { ConfirmationDialog } from '../../shared/components/ConfirmationDialog';
import { useSignQr } from './useSignQr';
import { preventSignSubmit, useSignLocation, useSignRadius } from './useSignSettings';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Link } from 'react-router';
import { type AdminSign } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { linkClass } from '../../shared/components/styles';
import { formatCoordinates, signPlaceLabel } from '../../shared/utils/labels';
import { formatDate, formatTime } from '../../shared/utils/thaiTime';

interface SignPanelProps {
  areaId: number;
  sign: AdminSign;
  /** What the sign is for, in the new-QR question: the point's name, or "ป้ายลงเวลา". */
  label: string;
  /** facility-0021 rule 4: no new QR for a deactivated point or Area (the server answers 409). */
  canIssueQr: boolean;
}

type NoteKind = { kind: 'ok' | 'error'; text: string } | null;

function NoteLine({ note }: { note: NoteKind }) {
  if (!note) {
    return null;
  }
  return note.kind === 'ok' ? <Note tone="info" status>{note.text}</Note> : <Note tone="error">{note.text}</Note>;
}

// Radius stays visible; infrequent location and QR actions live in labelled details. Each save remains independent.
const panelsClass = 'flex min-w-0 flex-col gap-4';
const partClass = 'flex flex-col gap-2 rounded-md border border-border p-3';
const rowClass = 'flex flex-wrap items-end gap-2';

/**
 * Wireframe D4's sign settings (facility-0045, 0038, 0006): three small forms, each with its own save, so no save is
 * ever half done (plan 10 decision 6). The parent keys it by sign id.
 */
export function SignPanel({ areaId, sign, label, canIssueQr }: SignPanelProps) {
  return (
    <div className={panelsClass} onKeyDown={preventSignSubmit}>
      <SignRadius sign={sign} />
      <MoreInfo summary="พิกัดและ QR ของป้าย">
        <div className="flex flex-col gap-3">
          <SignLocation areaId={areaId} sign={sign} />
          <SignQr areaId={areaId} sign={sign} label={label} canIssueQr={canIssueQr} />
        </div>
      </MoreInfo>
    </div>
  );
}

/** facility-0045 items 2 and 3: captured at the sign on a phone (M17), or typed from a map before going there. */
function SignLocation({ areaId, sign }: { areaId: number; sign: AdminSign }) {
  const { text, setText, note, isLoading, titleId, saveMapPick } = useSignLocation(sign);

  return (
    <section className={partClass} aria-labelledby={titleId}>
      <h4 id={titleId} className="font-semibold">
        พิกัดของป้าย
      </h4>
      <p>
        {sign.latitude != null && sign.longitude != null
          ? `${signPlaceLabel(sign)} · ${formatCoordinates(sign.latitude, sign.longitude)}`
          : signPlaceLabel(sign)}
      </p>
      <Link className={linkClass} to={`/admin/areas/${areaId}/capture?sign=${sign.id}`}>
        เก็บพิกัดที่ป้ายด้วยมือถือ (M17) ›
      </Link>
      <p className="text-text-2">
        เปิดหน้านี้บนมือถือ ยืนตรงที่ติดป้าย แล้วกด "ใช้ตำแหน่งตรงนี้" ระบบอ่านตำแหน่งต่อเนื่อง 30 วินาที แล้วใช้ค่ากลาง
        ให้ยืนนิ่งจนครบเวลา
      </p>
      {sign.locationSource !== 'Site' && (
        <>
          <div className={rowClass}>
            <Field label="ยังไม่ได้ไปที่ป้าย? ปักพิกัดจากแผนที่ไปก่อน">
              <TextInput
                size="desk"
                placeholder="13.756300, 100.501800"
                value={text}
                disabled={isLoading}
                onChange={(event) => setText(event.target.value)}
              />
            </Field>
            <Button size="desk" disabled={isLoading} onClick={() => void saveMapPick()}>
              บันทึกพิกัดที่ปักจากแผนที่
            </Button>
          </div>
          <p className="text-text-2">
            คัดลอกพิกัดจากแอปแผนที่ พิกัดที่ปักจากแผนที่จะขึ้นว่า "ยังไม่ได้วัดที่ป้ายจริง" จนกว่าจะมาวัดที่ป้าย
          </p>
        </>
      )}
      <NoteLine note={note} />
    </section>
  );
}

/** facility-0038: 50 m by default, set per sign. */
function SignRadius({ sign }: { sign: AdminSign }) {
  const { text, setText, note, isLoading, titleId, save } = useSignRadius(sign);

  return (
    <section className="flex flex-col gap-2" aria-labelledby={titleId}>
      <h4 id={titleId} className="sr-only">
        รัศมีของป้าย
      </h4>
      <div className={rowClass}>
        <Field label="รัศมี (เมตร)">
          {/* a number of 2–3 digits; no token names that width */}
          <TextInput
            size="desk"
            className="w-28 text-right"
            inputMode="numeric"
            value={text}
            disabled={isLoading}
            onChange={(event) => setText(event.target.value)}
          />
        </Field>
        <Button size="desk" disabled={isLoading} onClick={() => void save()}>
          บันทึกรัศมี
        </Button>
      </div>
      <p className="text-text-2">10–500 เมตร · สแกนห่างจากป้ายเกินรัศมีจะขึ้นว่าไกลป้าย ค่าเริ่ม 50 ม. · บันทึกรัศมีแยกจากข้อมูลพื้นที่หรือจุด</p>
      <NoteLine note={note} />
    </section>
  );
}

/** facility-0021 item 5: confirm before invalidating the sign, then optionally open the existing print route. */
function SignQr({ areaId, sign, label, canIssueQr }: SignPanelProps) {
  const { done, confirmation, ask } = useSignQr(areaId, sign.id);
  const titleId = `sign-${sign.id}-qr`;

  return (
    <section className={partClass} aria-labelledby={titleId}>
      <h4 id={titleId} className="font-semibold">
        QR ของป้าย
      </h4>
      <p>{`สร้าง QR ล่าสุด ${formatDate(sign.qrIssuedAt)} ${formatTime(sign.qrIssuedAt)}`}</p>
      {!canIssueQr && <p className="text-text-2">ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้</p>}
      {canIssueQr && <div><Button size="desk" onClick={ask}>สร้าง QR ใหม่</Button></div>}
      {confirmation.question && <ConfirmationDialog
          kind="qr"
          title={`สร้าง QR ใหม่ของป้าย ${sign.code} ${label}?`}
          body="ป้ายที่ติดอยู่ตอนนี้จะสแกนไม่ได้ทันที ต้องพิมพ์ป้ายใหม่ไปติดแทน"
          impacts={['ประวัติการสแกนไม่เปลี่ยน']}
          confirmLabel="ยืนยัน สร้าง QR ใหม่"
          printFollowup
          onCancel={confirmation.cancel}
          onConfirm={confirmation.confirm}
        />}
      {done && (
        <Note tone="info" status>
          สร้าง QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว{' '}
          <Link className={linkClass} to={`/admin/print?area=${areaId}&sign=${sign.id}`}>
            พิมพ์ป้ายใหม่ ›
          </Link>
        </Note>
      )}
    </section>
  );
}
