import { useId, useRef, useState } from 'react';
import { getErrorMessage } from '../utils/getErrorMessage';

/** Each mounted question owns its draft and failed request, so retry never loses the typed code. */
export function useConfirmationDialog(
  onConfirm: (print: boolean) => Promise<void>, onCancel: () => void, code?: string,
) {
  const id = useId();
  const [typed, setTyped] = useState('');
  const [print, setPrint] = useState(true);
  const [pending, setPending] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const lock = useRef(false);
  const cancel = () => {
    if (!lock.current) onCancel();
  };
  const confirm = async () => {
    if (lock.current || (code !== undefined && typed !== code)) return;
    lock.current = true;
    setPending(true);
    setProblem(null);
    try {
      await onConfirm(print);
    } catch (error) {
      setProblem(getErrorMessage(error));
    } finally {
      lock.current = false;
      setPending(false);
    }
  };
  return {
    id, typed, setTyped, print, setPrint, pending, problem, cancel, confirm,
    disabled: pending || (code !== undefined && typed !== code),
  };
}

/** The action remains deferred until the shared question calls it. Rejection keeps the question open. */
export function useConfirmationAction() {
  const [question, setQuestion] = useState<{ run: (print: boolean) => Promise<void> } | null>(null);
  const ask = (run: (print: boolean) => Promise<void>) => setQuestion({ run });
  const cancel = () => setQuestion(null);
  const confirm = async (print: boolean) => {
    await question?.run(print);
    setQuestion(null);
  };
  return { question, ask, cancel, confirm };
}
