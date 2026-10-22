import { useState } from 'react';
import { Link } from 'react-router';
import { useSetSignLocationMutation, type AdminSign } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatCoordinates, signPlaceLabel } from '../../shared/utils/labels';
import { formatDate, formatTime } from '../../shared/utils/thaiTime';
import { useRegenerateQrMutation, useSetSignRadiusMutation } from './areasApi';
import { MAX_RADIUS_M, MIN_RADIUS_M, parseLatLng, parseRadius } from './signRules';

interface SignPanelProps {
  areaId: number;
  sign: AdminSign;
  /** What the sign is for, in the new-QR question: the point's name, or "ป้ายลงเวลา". */
  label: string;
  /** facility-0021 rule 4: no new QR for a deactivated point or Area (the server answers 409). */
  canIssueQr: boolean;
}

type Note = { kind: 'ok' | 'error'; text: string } | null;

function NoteLine({ note }: { note: Note }) {
  if (!note) {
    return null;
  }
  return (
    <p
      className={note.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
      role={note.kind === 'ok' ? 'status' : 'alert'}
    >
      {note.text}
    </p>
  );
}

/**
 * Wireframe D4's sign settings (facility-0045, 0038, 0006): three small forms, each with its own save, so no save is
 * ever half done (plan 10 decision 6). The parent keys it by sign id.
 */
export function SignPanel({ areaId, sign, label, canIssueQr }: SignPanelProps) {
  return (
    <div className="areas-sign">
      <SignLocation areaId={areaId} sign={sign} />
      <SignRadius sign={sign} />
      <SignQr areaId={areaId} sign={sign} label={label} canIssueQr={canIssueQr} />
    </div>
  );
}

/** facility-0045 items 2 and 3: captured at the sign on a phone (M17), or typed from a map before going there. */
function SignLocation({ areaId, sign }: { areaId: number; sign: AdminSign }) {
  const [text, setText] = useState('');
  const [note, setNote] = useState<Note>(null);
  const [setLocation, { isLoading }] = useSetSignLocationMutation();
  const titleId = `sign-${sign.id}-place`;

  const saveMapPick = async () => {
    const place = parseLatLng(text);
    if (!place) {
      setNote({ kind: 'error', text: 'ใส่พิกัดแบบ 13.756300, 100.501800' });
      return;
    }
    try {
      await setLocation({ signId: sign.id, ...place, source: 'Map' }).unwrap();
      setText('');
      setNote({ kind: 'ok', text: 'บันทึกพิกัดที่ปักจากแผนที่แล้ว ป้ายนี้ยังไม่ได้วัดที่ป้ายจริง' });
    } catch (error) {
      setNote({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <section className="areas-sign__part" aria-labelledby={titleId}>
      <h4 id={titleId}>พิกัดของป้าย</h4>
      <p>
        {sign.latitude != null && sign.longitude != null
          ? `${signPlaceLabel(sign)} · ${formatCoordinates(sign.latitude, sign.longitude)}`
          : signPlaceLabel(sign)}
      </p>
      <Link to={`/admin/areas/${areaId}/capture?sign=${sign.id}`}>เก็บพิกัดที่ป้ายด้วยมือถือ (M17) ›</Link>
      <p className="app-field__help">
        เปิดหน้านี้บนมือถือ ยืนตรงที่ติดป้าย แล้วกด "ใช้ตำแหน่งตรงนี้" ระบบอ่านตำแหน่งต่อเนื่อง 30 วินาที แล้วใช้ค่ากลาง
        ให้ยืนนิ่งจนครบเวลา
      </p>
      {sign.locationSource !== 'Site' && (
        <div className="areas-sign__row">
          <label className="app-field">
            ยังไม่ได้ไปที่ป้าย? ปักพิกัดจากแผนที่ไปก่อน
            <input
              className="app-input"
              placeholder="13.756300, 100.501800"
              value={text}
              disabled={isLoading}
              onChange={(event) => setText(event.target.value)}
            />
          </label>
          <button type="button" className="app-button" disabled={isLoading} onClick={() => void saveMapPick()}>
            บันทึกพิกัดที่ปักจากแผนที่
          </button>
          <p className="app-field__help">
            คัดลอกพิกัดจากแอปแผนที่ พิกัดที่ปักจากแผนที่จะขึ้นว่า "ยังไม่ได้วัดที่ป้ายจริง" จนกว่าจะมาวัดที่ป้าย
          </p>
        </div>
      )}
      <NoteLine note={note} />
    </section>
  );
}

/** facility-0038: 50 m by default, set per sign. */
function SignRadius({ sign }: { sign: AdminSign }) {
  const [text, setText] = useState(String(sign.radiusM));
  const [note, setNote] = useState<Note>(null);
  const [setRadius, { isLoading }] = useSetSignRadiusMutation();
  const titleId = `sign-${sign.id}-radius`;

  const save = async () => {
    const radiusM = parseRadius(text);
    if (radiusM === null) {
      setNote({ kind: 'error', text: `รัศมีต้องอยู่ระหว่าง ${MIN_RADIUS_M}–${MAX_RADIUS_M} เมตร` });
      return;
    }
    try {
      await setRadius({ signId: sign.id, radiusM }).unwrap();
      setNote({ kind: 'ok', text: `บันทึกรัศมี ${radiusM} ม. แล้ว ใช้กับการสแกนครั้งต่อไป` });
    } catch (error) {
      setNote({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <section className="areas-sign__part" aria-labelledby={titleId}>
      <h4 id={titleId}>รัศมีของป้าย</h4>
      <div className="areas-sign__row">
        <label className="app-field">
          รัศมี (เมตร)
          <input
            className="app-input areas-sign__radius"
            inputMode="numeric"
            value={text}
            disabled={isLoading}
            onChange={(event) => setText(event.target.value)}
          />
        </label>
        <button type="button" className="app-button" disabled={isLoading} onClick={() => void save()}>
          บันทึกรัศมี
        </button>
      </div>
      <p className="app-field__help">สแกนห่างจากป้ายเกินรัศมีจะขึ้นว่าไกลป้าย ค่าเริ่ม 50 ม.</p>
      <NoteLine note={note} />
    </section>
  );
}

/** facility-0021 item 5, 0006: one question in the page, then the offer to print the new sign. */
function SignQr({ areaId, sign, label, canIssueQr }: SignPanelProps) {
  const [asking, setAsking] = useState(false);
  const [done, setDone] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [regenerate, { isLoading }] = useRegenerateQrMutation();
  const titleId = `sign-${sign.id}-qr`;

  const confirm = async () => {
    try {
      await regenerate(sign.id).unwrap();
      setAsking(false);
      setDone(true);
      setProblem(null);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="areas-sign__part" aria-labelledby={titleId}>
      <h4 id={titleId}>QR ของป้าย</h4>
      <p>{`สร้าง QR ล่าสุด ${formatDate(sign.qrIssuedAt)} ${formatTime(sign.qrIssuedAt)}`}</p>
      {!canIssueQr && <p className="app-field__help">ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้</p>}
      {canIssueQr && !asking && (
        <button
          type="button"
          className="app-button areas-sign__qr"
          onClick={() => {
            setAsking(true);
            setDone(false);
          }}
        >
          สร้าง QR ใหม่
        </button>
      )}
      {asking && (
        <div
          className="app-confirm"
          role="alertdialog"
          aria-labelledby={`${titleId}-ask`}
          aria-describedby={`${titleId}-detail`}
        >
          <p id={`${titleId}-ask`} className="app-confirm__title">{`สร้าง QR ใหม่ของป้าย ${sign.code} ${label}?`}</p>
          <p id={`${titleId}-detail`}>ป้ายที่ติดอยู่ตอนนี้จะสแกนไม่ได้ทันที ต้องพิมพ์ป้ายใหม่ไปติดแทน ประวัติการสแกนไม่เปลี่ยน</p>
          <div className="app-confirm__buttons">
            <button
              type="button"
              className="app-button app-button--warn"
              disabled={isLoading}
              onClick={() => void confirm()}
            >
              {isLoading ? 'กำลังสร้าง QR...' : 'ยืนยัน สร้าง QR ใหม่'}
            </button>
            {/* The safe choice has the focus */}
            <button type="button" className="app-button" autoFocus disabled={isLoading} onClick={() => setAsking(false)}>
              ยกเลิก
            </button>
          </div>
        </div>
      )}
      {done && (
        <p className="app-note app-note--info" role="status">
          <span>สร้าง QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว</span>{' '}
          <Link to={`/admin/print?area=${areaId}&sign=${sign.id}`}>พิมพ์ป้ายใหม่ ›</Link>
        </p>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
    </section>
  );
}
