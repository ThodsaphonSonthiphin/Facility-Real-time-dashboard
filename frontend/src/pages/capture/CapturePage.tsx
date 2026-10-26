import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import {
  useGetAreaQuery,
  useSetSignLocationMutation,
  type AdminSign,
  type AreaDetail,
} from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { boxClass, focusRing, linkClass } from '../../shared/components/styles';
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

// A row of the sign list: a 48 px target that says which sign is the one being read
const rowClass = `flex min-h-12 w-full flex-col items-start gap-1 rounded border px-3 py-2.5 text-left ${focusRing}`;
const rowIdleClass = 'border-input-border bg-surface text-text hover:border-primary';
const rowChosenClass = 'border-primary bg-info-bg text-text';

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
    // M17 is used at the sign, so it takes the phone sizes (pages/admin.md, #79)
    <main className="flex flex-col gap-4 text-base text-text">
      <Link className={linkClass} to={`/admin/areas/${areaId}`}>
        ‹ กลับไปหน้าจัดการพื้นที่และป้าย
      </Link>
      {detail && (
        <span className="text-md tracking-wide text-muted">{`แอดมิน · เก็บพิกัดป้าย · ${detail.area.code} ${detail.area.name}`}</span>
      )}
      <h1 className="text-2xl font-bold">เก็บพิกัดป้าย</h1>
      {area.error && <Note tone="error">{getErrorMessage(area.error)}</Note>}
      {!detail && !area.error && <p className="text-muted">กำลังโหลด...</p>}
      {detail && (
        <CaptureWorkspace
          key={selected?.sign.id ?? 'none'}
          signs={signs}
          selected={selected}
          onChoose={(signId) => setParams({ sign: String(signId) })}
        />
      )}
      <p className="text-md text-muted">ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติการแก้ไขของแอดมิน</p>
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
      {!selected && <Note tone="info">ยืนตรงที่ติดป้าย แล้วเลือกป้ายนั้นจากรายการข้างล่าง</Note>}
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
      <section className="flex flex-col gap-2" aria-labelledby="capture-signs-title">
        <h2 id="capture-signs-title" className="text-lg font-semibold">
          ป้ายของพื้นที่นี้
        </h2>
        <ul className="m-0 flex list-none flex-col gap-2 p-0" aria-label="ป้ายของพื้นที่นี้">
          {signs.map((target) => {
            const isSelected = target.sign.id === selected?.sign.id;
            return (
              <li key={target.sign.id}>
                <button
                  type="button"
                  data-tap=""
                  className={`${rowClass} ${isSelected ? rowChosenClass : rowIdleClass}`}
                  aria-pressed={isSelected}
                  onClick={() => onChoose(target.sign.id)}
                >
                  <strong className="font-semibold">{`${target.sign.code} ${target.title}`}</strong>
                  <span className="text-md text-muted">
                    {isSelected && phase.kind === 'reading' ? 'กำลังเก็บ' : signPlaceLabel(target.sign)}
                  </span>
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
  const verdict = result?.kind === 'fix' ? accuracyVerdict(result.fix.accuracyM, sign.radiusM) : undefined;

  return (
    <section className={`flex flex-col gap-2.5 ${boxClass}`} aria-labelledby="capture-sign-title">
      <h2 id="capture-sign-title" className="text-lg font-bold">{`${sign.code} ${title}`}</h2>
      <p className="text-md text-muted">{`รัศมี ${sign.radiusM} ม. · ตอนนี้: ${signPlaceLabel(sign)}`}</p>
      {phase.kind === 'reading' && (
        <>
          <p className="text-xl font-bold" role="status">
            {`กำลังอ่านตำแหน่ง ${phase.progress.seconds} / ${SECONDS} วินาที`}
          </p>
          <p>
            {`ยืนนิ่งตรงที่ติดป้าย · อ่านได้ ${phase.progress.readings} ครั้ง${
              phase.progress.accuracyM != null ? ` · ความแม่นยำตอนนี้ ±${Math.round(phase.progress.accuracyM)} ม.` : ''
            }`}
          </p>
          <Button variant="primary" size="main" disabled>
            {`บันทึกพิกัด (รอครบ ${SECONDS} วินาที)`}
          </Button>
        </>
      )}
      {result?.kind === 'fix' && (
        <>
          <p role="status">{`อ่านครบแล้ว ${result.readings} ครั้ง ใน ${SECONDS} วินาที`}</p>
          <p>{`ค่ากลาง: ${formatCoordinates(result.fix.latitude, result.fix.longitude)}`}</p>
          {verdict && <Note tone={verdict.ok ? 'info' : 'warn'}>{verdict.text}</Note>}
          <div className="flex flex-col gap-2">
            <Button variant="primary" size="main" disabled={saving} onClick={() => onSave(result.fix)}>
              {saving ? 'กำลังบันทึก...' : 'บันทึกพิกัด'}
            </Button>
            {/* The reading again is not the page's main action (pages/phone.md), so it is outlined, still full width */}
            <Button variant="secondary" size="phone" className="w-full" disabled={saving} onClick={onStart}>
              เก็บใหม่
            </Button>
          </div>
        </>
      )}
      {result?.kind === 'too-few' && (
        <Note tone="warn">
          {`อ่านตำแหน่งได้แค่ ${result.readings} ครั้งใน ${SECONDS} วินาที ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วเก็บใหม่`}
        </Note>
      )}
      {result?.kind === 'denied' && (
        <Note tone="error">เปิดตำแหน่งและอนุญาตให้เว็บนี้อ่านตำแหน่ง แล้วกด "ใช้ตำแหน่งตรงนี้" อีกครั้ง</Note>
      )}
      {result?.kind === 'unavailable' && (
        <Note tone="error">เครื่องนี้อ่านตำแหน่งไม่ได้ เปิดหน้านี้บนมือถือที่อ่านตำแหน่งได้</Note>
      )}
      {result?.kind === 'insecure' && <Note tone="error">หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง</Note>}
      {phase.kind === 'saved' && (
        <Note tone="info" status>
          {phase.text}
        </Note>
      )}
      {problem && <Note tone="error">{problem}</Note>}
      {showStart && (
        <Button variant="primary" size="main" onClick={onStart}>
          {readAgain ? 'เก็บใหม่' : 'ใช้ตำแหน่งตรงนี้'}
        </Button>
      )}
    </section>
  );
}
