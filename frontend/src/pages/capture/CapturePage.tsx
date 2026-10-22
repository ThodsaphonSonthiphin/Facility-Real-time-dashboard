import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import {
  useGetAreaQuery,
  useSetSignLocationMutation,
  type AdminSign,
  type AreaDetail,
} from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import type { GpsFix } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatCoordinates, signPlaceLabel } from '../../shared/utils/labels';
import {
  accuracyVerdict,
  CAPTURE_MS,
  startCapture,
  type CaptureProgress,
  type CaptureResult,
} from './captureLocation';
import './capture.css';

type Phase =
  | { kind: 'idle' }
  | { kind: 'reading'; progress: CaptureProgress }
  | { kind: 'result'; result: CaptureResult }
  | { kind: 'saved'; text: string };

interface CaptureSign {
  sign: AdminSign;
  title: string;
}

const SECONDS = CAPTURE_MS / 1000;

/** The Area's signs that can be scanned: the check-in sign and the active points (D4's order). */
function captureSigns(detail: AreaDetail): CaptureSign[] {
  return [
    { sign: detail.checkInSign, title: 'ป้ายลงเวลา' },
    ...detail.points.filter((point) => point.isActive).map((point) => ({ sign: point.sign, title: point.name })),
  ];
}

/**
 * facility-0045 item 2, wireframe M17: the Admin stands at a sign with a phone, reads the GPS for 30 s and saves the
 * median as a site location. The reading belongs to the sign in the URL: the workspace is keyed by that sign, so any
 * change of `?sign=` (a choice, Back, navigate) unmounts it, which stops a running GPS and drops its result.
 */
export function CapturePage() {
  const { areaId } = useParams();
  const [params, setParams] = useSearchParams();
  const area = useGetAreaQuery(Number(areaId), freshOnOpen);
  const detail = area.currentData;
  const signs = detail ? captureSigns(detail) : [];
  const selected = signs.find((target) => String(target.sign.id) === params.get('sign'));

  return (
    <main className="capture-page">
      <Link to={`/admin/areas/${areaId}`}>‹ กลับไปหน้าจัดการพื้นที่และป้าย</Link>
      {detail && <span className="capture-kicker">{`แอดมิน · เก็บพิกัดป้าย · ${detail.area.code} ${detail.area.name}`}</span>}
      <h1>เก็บพิกัดป้าย</h1>
      {area.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(area.error)}
        </p>
      )}
      {detail && (
        <CaptureWorkspace
          key={selected?.sign.id ?? 'none'}
          signs={signs}
          selected={selected}
          onChoose={(signId) => setParams({ sign: String(signId) })}
        />
      )}
      <p className="app-muted">ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติการแก้ไขของแอดมิน</p>
    </main>
  );
}

interface CaptureWorkspaceProps {
  signs: CaptureSign[];
  selected: CaptureSign | undefined;
  onChoose: (signId: number) => void;
}

/** Owns one sign's reading, its result and its save. Mounted per sign (see CapturePage), so its state never outlives it. */
function CaptureWorkspace({ signs, selected, onChoose }: CaptureWorkspaceProps) {
  const [setLocation, saving] = useSetSignLocationMutation();
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [problem, setProblem] = useState<string | null>(null);
  const [runId, setRunId] = useState(0);

  useEffect(() => {
    if (runId === 0) {
      return undefined;
    }
    let live = true;
    const run = startCapture((progress) => {
      if (live) {
        setPhase({ kind: 'reading', progress });
      }
    });
    void run.done.then((result) => {
      if (live) {
        setPhase({ kind: 'result', result });
      }
    });
    return () => {
      live = false;
      run.cancel();
    };
  }, [runId]);

  const start = () => {
    setProblem(null);
    setPhase({ kind: 'reading', progress: { seconds: 0, readings: 0, accuracyM: null } });
    setRunId((id) => id + 1);
  };
  const save = async (sign: AdminSign, fix: GpsFix) => {
    setProblem(null);
    try {
      const saved = await setLocation({
        signId: sign.id,
        latitude: fix.latitude,
        longitude: fix.longitude,
        accuracyM: fix.accuracyM,
        source: 'Site',
      }).unwrap();
      setPhase({ kind: 'saved', text: `บันทึกพิกัดป้าย ${saved.code} แล้ว ${signPlaceLabel(saved)}` });
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <>
      {!selected && <p className="app-note app-note--info">ยืนตรงที่ติดป้าย แล้วเลือกป้ายนั้นจากรายการข้างล่าง</p>}
      {selected && (
        <SignCapture
          target={selected}
          phase={phase}
          problem={problem}
          saving={saving.isLoading}
          onStart={start}
          onSave={(fix) => void save(selected.sign, fix)}
        />
      )}
      <section className="capture-signs" aria-labelledby="capture-signs-title">
        <h2 id="capture-signs-title">ป้ายของพื้นที่นี้</h2>
        <ul aria-label="ป้ายของพื้นที่นี้">
          {signs.map((target) => {
            const isSelected = target.sign.id === selected?.sign.id;
            return (
              <li key={target.sign.id}>
                <button
                  type="button"
                  className={isSelected ? 'capture-row capture-row--selected' : 'capture-row'}
                  aria-pressed={isSelected}
                  onClick={() => onChoose(target.sign.id)}
                >
                  <strong>{`${target.sign.code} ${target.title}`}</strong>
                  <span>{isSelected && phase.kind === 'reading' ? 'กำลังเก็บ' : signPlaceLabel(target.sign)}</span>
                </button>
              </li>
            );
          })}
        </ul>
      </section>
    </>
  );
}

interface SignCaptureProps {
  target: CaptureSign;
  phase: Phase;
  problem: string | null;
  saving: boolean;
  onStart: () => void;
  onSave: (fix: GpsFix) => void;
}

/** Wireframe M17's card: the sign, the 30 s reading, the median with its accuracy, and the save. */
function SignCapture({ target, phase, problem, saving, onStart, onSave }: SignCaptureProps) {
  const { sign, title } = target;
  const result = phase.kind === 'result' ? phase.result : null;
  const readAgain = phase.kind === 'saved' || result?.kind === 'too-few';
  const showStart = phase.kind !== 'reading' && result?.kind !== 'fix' && result?.kind !== 'insecure';

  return (
    <section className="app-panel capture-sign" aria-labelledby="capture-sign-title">
      <h2 id="capture-sign-title">{`${sign.code} ${title}`}</h2>
      <p className="app-muted">{`รัศมี ${sign.radiusM} ม. · ตอนนี้: ${signPlaceLabel(sign)}`}</p>
      {phase.kind === 'reading' && (
        <>
          <p className="capture-progress" role="status">
            {`กำลังอ่านตำแหน่ง ${phase.progress.seconds} / ${SECONDS} วินาที`}
          </p>
          <p>
            {`ยืนนิ่งตรงที่ติดป้าย · อ่านได้ ${phase.progress.readings} ครั้ง${
              phase.progress.accuracyM != null ? ` · ความแม่นยำตอนนี้ ±${Math.round(phase.progress.accuracyM)} ม.` : ''
            }`}
          </p>
          <button type="button" className="app-button app-button--primary" disabled>
            {`บันทึกพิกัด (รอครบ ${SECONDS} วินาที)`}
          </button>
        </>
      )}
      {result?.kind === 'fix' && (
        <>
          <p role="status">{`อ่านครบแล้ว ${result.readings} ครั้ง ใน ${SECONDS} วินาที`}</p>
          <p>{`ค่ากลาง: ${formatCoordinates(result.fix.latitude, result.fix.longitude)}`}</p>
          <p className={accuracyVerdict(result.fix.accuracyM, sign.radiusM).ok ? 'app-note app-note--info' : 'app-note app-note--warn'}>
            {accuracyVerdict(result.fix.accuracyM, sign.radiusM).text}
          </p>
          <div className="capture-buttons">
            <button type="button" className="app-button app-button--primary" disabled={saving} onClick={() => onSave(result.fix)}>
              {saving ? 'กำลังบันทึก...' : 'บันทึกพิกัด'}
            </button>
            <button type="button" className="app-button" disabled={saving} onClick={onStart}>
              เก็บใหม่
            </button>
          </div>
        </>
      )}
      {result?.kind === 'too-few' && (
        <p className="app-note app-note--warn">
          {`อ่านตำแหน่งได้แค่ ${result.readings} ครั้งใน ${SECONDS} วินาที ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วเก็บใหม่`}
        </p>
      )}
      {result?.kind === 'denied' && (
        <p className="app-note app-note--error" role="alert">
          เปิดตำแหน่งและอนุญาตให้เว็บนี้อ่านตำแหน่ง แล้วกด "ใช้ตำแหน่งตรงนี้" อีกครั้ง
        </p>
      )}
      {result?.kind === 'unavailable' && (
        <p className="app-note app-note--error" role="alert">
          เครื่องนี้อ่านตำแหน่งไม่ได้ เปิดหน้านี้บนมือถือที่อ่านตำแหน่งได้
        </p>
      )}
      {result?.kind === 'insecure' && (
        <p className="app-note app-note--error" role="alert">
          หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง
        </p>
      )}
      {phase.kind === 'saved' && (
        <p className="app-note app-note--info" role="status">
          {phase.text}
        </p>
      )}
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {showStart && (
        <button type="button" className="app-button app-button--primary" onClick={onStart}>
          {readAgain ? 'เก็บใหม่' : 'ใช้ตำแหน่งตรงนี้'}
        </button>
      )}
    </section>
  );
}
