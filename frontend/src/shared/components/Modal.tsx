import { useLayoutEffect, useRef, type ReactNode } from 'react';
import { boxClass } from './styles';

interface ModalProps {
  /** The id of the heading that names the dialog. */
  labelledBy: string;
  /** Escape (or a phone's back gesture) asks to close; the parent decides, so a send in flight can keep it open. */
  onClose: () => void;
  children: ReactNode;
}

/**
 * A form over the page (the browser's own modal `<dialog>`): the page behind cannot be used and does not move, so the
 * Admin keeps their place in the table (owner, #78). Rendered while open; the parent unmounts it to close it.
 */
export function Modal({ labelledBy, onClose, children }: ModalProps) {
  const ref = useRef<HTMLDialogElement>(null);

  // A layout effect: its cleanup runs before the dialog leaves the page, so the keyboard never lands on <body>
  useLayoutEffect(() => {
    const dialog = ref.current;
    // Whatever opened it gets the keyboard back on close, so the Admin is where they were in the table
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog?.showModal();
    return () => {
      dialog?.close();
      if (opener?.isConnected) {
        opener.focus();
      }
    };
  }, []);

  return (
    <dialog
      ref={ref}
      aria-labelledby={labelledBy}
      // The browser would close the dialog itself; the parent unmounts it instead, so React's state stays the truth
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          onClose();
        }
      }}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      // text-text: a dialog does not inherit the colour (the browser sets its own). w-160 is 640 px, the form's old width; the calc keeps a 16 px gutter on a narrow window; no token describes that
      className={`m-auto w-160 max-w-[calc(100%-2rem)] text-text backdrop:bg-text/40 ${boxClass}`}
    >
      {children}
    </dialog>
  );
}
