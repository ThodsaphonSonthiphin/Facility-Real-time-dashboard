import { useState, type KeyboardEvent } from 'react';
import { useSetSignLocationMutation, type AdminSign } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useSetSignRadiusMutation } from './areasApi';
import { MAX_RADIUS_M, MIN_RADIUS_M, parseLatLng, parseRadius } from './signRules';
export type SignNote = { kind: 'ok' | 'error'; text: string } | null;
type NoteKind = SignNote;
export function useSignLocation(sign: AdminSign) {
  const [text, setText] = useState('');
  const [note, setNote] = useState<NoteKind>(null);
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

return { text, setText, note, isLoading, titleId, saveMapPick };
}

export function useSignRadius(sign: AdminSign) {
  const [text, setText] = useState(String(sign.radiusM));
  const [note, setNote] = useState<NoteKind>(null);
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

return { text, setText, note, isLoading, titleId, save };
}

/** Enter in independent sign settings must not submit the surrounding Area/point form. */
export function preventSignSubmit(event: KeyboardEvent<HTMLElement>) {
  if (event.key === 'Enter' && event.target instanceof HTMLInputElement) event.preventDefault();
}
