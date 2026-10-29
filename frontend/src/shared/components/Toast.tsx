import { CircleAlert, CircleCheck, TriangleAlert, X, type LucideIcon } from 'lucide-react';
import { useEffect, useEffectEvent, useState } from 'react';
import { focusRing, noteTones, type NoteTone } from './styles';

/**
 * The answer to an Admin's action (D2, D3): it outlives the form or item that sent it. ok: it was done; warn: something
 * the Admin did not ask for happened, such as their form closing; error: it failed.
 */
export type Notice = { kind: 'ok' | 'warn' | 'error'; text: string };

/** How long a done notice stays, then how long it takes to fade (owner, #78: 3–5 s). */
const shownMs = 5_000;
const fadeMs = 200;

const looks: Record<Notice['kind'], { tone: NoteTone; Icon: LucideIcon; role: 'status' | 'alert' }> = {
  ok: { tone: 'info', Icon: CircleCheck, role: 'status' },
  warn: { tone: 'warn', Icon: TriangleAlert, role: 'status' },
  error: { tone: 'error', Icon: CircleAlert, role: 'alert' },
};

interface ToastProps {
  notice: Notice;
  /** ✕, or the 5 s running out: the page drops the notice. */
  onClose: () => void;
}

/**
 * A notice floating at the top centre over the page, so nothing under it moves (owner, #78). Done goes by itself after
 * 5 s, held while the pointer or the keyboard is on it; warn and error stay until ✕, so they are not missed. Done and
 * warn are `role="status"` (read out politely), error `role="alert"`. The words sit on the element with the role, so
 * the role is found from the words. ✕ is the one icon-only button (owner, #78), named ปิดข้อความ for screen readers.
 */
export function Toast({ notice, onClose }: ToastProps) {
  const { tone, Icon, role } = looks[notice.kind];
  const goes = notice.kind === 'ok';
  const [pointerOn, setPointerOn] = useState(false);
  const [keyboardOn, setKeyboardOn] = useState(false);
  const [fading, setFading] = useState(false);
  const [timed, setTimed] = useState(notice);
  // A new notice starts its 5 s afresh
  if (timed !== notice) {
    setTimed(notice);
    setFading(false);
  }
  const held = pointerOn || keyboardOn;
  // The page passes a new onClose on every render (a live board renders often); that must not restart the 5 s
  const close = useEffectEvent(onClose);

  useEffect(() => {
    if (!goes || held) {
      return;
    }
    const fade = setTimeout(() => setFading(true), shownMs);
    const gone = setTimeout(() => close(), shownMs + fadeMs);
    return () => {
      clearTimeout(fade);
      clearTimeout(gone);
    };
  }, [notice, goes, held]);

  return (
    <div
      className={`fixed top-4 right-4 left-4 z-10 mx-auto flex max-w-96 items-center gap-2.5 rounded-lg border py-1 pr-1 pl-3.5 shadow-sm transition-opacity starting:opacity-0 motion-reduce:transition-none ${noteTones[tone]} ${fading ? 'opacity-0' : 'opacity-100'}`}
      onMouseEnter={() => {
        setPointerOn(true);
        setFading(false);
      }}
      onMouseLeave={() => setPointerOn(false)}
      onFocus={() => {
        setKeyboardOn(true);
        setFading(false);
      }}
      onBlur={(event) => setKeyboardOn(event.currentTarget.contains(event.relatedTarget))}
    >
      <Icon aria-hidden="true" className="size-4 shrink-0" />
      <p className="m-0 min-w-0 flex-1" role={role}>
        {notice.text}
      </p>
      <button
        type="button"
        data-tap=""
        aria-label="ปิดข้อความ"
        className={`inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-lg border-0 bg-transparent text-current ${focusRing}`}
        onClick={onClose}
      >
        <X aria-hidden="true" className="size-4" />
      </button>
    </div>
  );
}
