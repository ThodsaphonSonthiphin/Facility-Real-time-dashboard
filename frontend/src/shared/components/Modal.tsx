import { useLayoutEffect, useRef, type ReactNode } from 'react';
import { boxClass } from './styles';

interface DialogProps {
  /** The id of the heading that names the dialog; give it `tabIndex={-1}`: it takes the keyboard when the form opens. */
  labelledBy: string;
  /** Escape (or a phone's back gesture) asks to close; the parent decides, so a send in flight can keep it open. */
  onClose: () => void;
  children: ReactNode;
}

/**
 * A form over the page (the browser's own modal `<dialog>`): the page behind cannot be used and does not move, so the
 * Admin keeps their place in the table (owner, #78). Rendered while open; the parent unmounts it to close it.
 * `Modal` is the box in the middle of the page, `Drawer` the one on its right edge.
 */
function DialogFrame({ labelledBy, onClose, className, children }: DialogProps & { className: string }) {
  const ref = useRef<HTMLDialogElement>(null);

  // A layout effect: its cleanup runs before the dialog leaves the page, so the keyboard never lands on <body>
  useLayoutEffect(() => {
    const dialog = ref.current;
    // Whatever opened it gets the keyboard back on close, so the Admin is where they were in the table
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog?.showModal();
    // Keyboard and screen-reader users start at the form's name, and Escape reaches the dialog
    document.getElementById(labelledBy)?.focus();
    return () => {
      dialog?.close();
      if (opener?.isConnected) {
        opener.focus();
      }
    };
  }, [labelledBy]);

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
      // text-text: a dialog does not inherit the colour (the browser sets its own)
      className={`text-text backdrop:bg-text/40 ${className}`}
    >
      {children}
    </dialog>
  );
}

/**
 * The page's own box in the middle of the page, 640 px: D2's correction form, D3's cover form. `w-160` is 640 px, the
 * form's old width; the calc keeps a 16 px gutter on a narrow window; no token describes that.
 */
export function Modal({ labelledBy, onClose, children }: DialogProps) {
  return (
    <DialogFrame
      labelledBy={labelledBy}
      onClose={onClose}
      className={`m-auto w-160 max-w-[calc(100%-2rem)] ${boxClass}`}
    >
      {children}
    </DialogFrame>
  );
}

/**
 * A panel sliding in from the right edge, full height, scrolling on its own (owner, 2026-10-23): the Area's table
 * stays visible on the left while a point or the Area itself is edited on the right. `w-120` is 480 px, half a 1280 px
 * screen's content beside the 300 px Area list; no token describes that. Only the left edge has a border, so the panel
 * looks attached to the screen's edge.
 */
export function Drawer({ labelledBy, onClose, children }: DialogProps) {
  return (
    <DialogFrame
      labelledBy={labelledBy}
      onClose={onClose}
      className="m-0 ml-auto h-dvh max-h-none w-120 max-w-[calc(100%-1rem)] overflow-y-auto rounded-none border-l border-border bg-surface px-4 py-3.5"
    >
      {children}
    </DialogFrame>
  );
}
