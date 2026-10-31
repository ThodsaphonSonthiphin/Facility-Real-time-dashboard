import { X } from 'lucide-react';
import { useLayoutEffect, useRef, type ReactNode } from 'react';
import { Button } from './Button';

interface DialogProps {
  /** The id of the heading that names the dialog; give it `tabIndex={-1}`: it takes the keyboard when the form opens. */
  labelledBy: string;
  /** `alertdialog` for a confirm of something that cannot be undone; the browser's `dialog` otherwise. */
  role?: 'alertdialog';
  /** The id of the text that explains an `alertdialog`, read out after its name. */
  describedBy?: string;
  /** Escape (or a phone's back gesture) asks to close; the parent decides, so a send in flight can keep it open. */
  onClose: () => void;
  children: ReactNode;
}

/**
 * The design file's widths: 480 px for most forms and D3's cover form; 600 px for D2's correction form, whose table of
 * four times needs the room; 520 px for D2's cover Drawer (ฟอร์มแก้เวลา, ฟอร์มมอบหมายทำแทน). No token describes them.
 */
const modalWidths = { md: 'w-120', lg: 'w-150' } as const;
// The approved point/round editor is 560px, wider than the 520px Area editor; no container token describes it.
const drawerWidths = { md: 'w-120', lg: 'w-130', xl: 'w-140' } as const;
type Width = keyof typeof modalWidths;

/**
 * A form over the page (the browser's own modal `<dialog>`): the page behind cannot be used and does not move, so the
 * Admin keeps their place in the table (owner, #78). Rendered while open; the parent unmounts it to close it.
 * `Modal` is the box in the middle of the page, `Drawer` the one on its right edge.
 */
function DialogFrame({ labelledBy, describedBy, role, onClose, className, children }: DialogProps & { className: string }) {
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
      role={role}
      aria-labelledby={labelledBy}
      aria-describedby={describedBy}
      // The browser would close the dialog itself; the parent unmounts it instead, so React's state stays the truth
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          event.stopPropagation();
          onClose();
        }
      }}
      onCancel={(event) => {
        event.preventDefault();
        event.stopPropagation();
        onClose();
      }}
      // text-text: a dialog does not inherit the colour (the browser sets its own). The design file's dim page behind and
      // overlay shadow; preflight already removes the browser's frame, and border-0 repeats it on purpose
      className={`border-0 bg-surface text-text shadow-overlay backdrop:bg-backdrop ${className}`}
    >
      {children}
    </dialog>
  );
}

/**
 * The page's own box in the middle of the page: D2's correction form, D3's cover form, a confirm. 480 px wide at most
 * (DESIGN_SPEC §5), 600 px with `width="lg"` (the owner's later design file draws D2's correction form so); the calc
 * keeps a 16 px gutter on a narrow window, which no token describes. 8 px corners, 24/20 padding (the design file's
 * dialog body). It scrolls inside itself when the screen is too short.
 */
export function Modal({ labelledBy, describedBy, role, width = 'md', onClose, children }: DialogProps & { width?: Width }) {
  return (
    <DialogFrame
      labelledBy={labelledBy}
      describedBy={describedBy}
      role={role}
      onClose={onClose}
      className={`m-auto ${modalWidths[width]} max-w-[calc(100%-2rem)] rounded-lg px-6 py-5`}
    >
      {children}
    </DialogFrame>
  );
}

/**
 * A panel sliding in from the right edge, full height, scrolling on its own (owner, 2026-10-23): the Area's table
 * stays visible on the left while a point or the Area itself is edited on the right. `w-120` is 480 px, half a 1280 px
 * screen's content beside the 300 px Area list, and the design file's Drawer; `width="lg"` is D2's 520 px cover form.
 */
export function Drawer({ labelledBy, width = 'md', onClose, children }: DialogProps & { width?: keyof typeof drawerWidths }) {
  return (
    <DialogFrame
      labelledBy={labelledBy}
      onClose={onClose}
      className={`m-0 ml-auto h-dvh max-h-none ${drawerWidths[width]} max-w-[calc(100%-1rem)] overflow-y-auto rounded-none px-6 py-5 open:flex open:flex-col`}
    >
      {children}
    </DialogFrame>
  );
}

/**
 * The design file's dialog head: the name (with the `id` the dialog's `labelledBy` points at, taking the keyboard when
 * it opens), an optional line under it, and "ปิด" in words on the right. It reaches the dialog's edges and draws the
 * line under itself, so put it first inside a Modal or Drawer.
 */
export function DialogHeader({
  id,
  title,
  detail,
  onClose,
  closeDisabled = false,
}: {
  id: string;
  title: ReactNode;
  detail?: ReactNode;
  onClose: () => void;
  /** While a send is in flight, as the dialog's own Escape. */
  closeDisabled?: boolean;
}) {
  return (
    <div className="-mx-6 -mt-5 mb-5 flex items-start justify-between gap-3 border-b border-border px-6 py-4">
      <div className="flex min-w-0 flex-col">
        <h2 id={id} tabIndex={-1} className="text-lg font-bold">
          {title}
        </h2>
        {detail && <span className="text-xs text-text-2">{detail}</span>}
      </div>
      <Button variant="link" size="desk" disabled={closeDisabled} onClick={onClose}>
        <X aria-hidden="true" className="size-4" />
        ปิด
      </Button>
    </div>
  );
}

/** The design file's dialog foot: its buttons on the right, ยกเลิก before the main one; put it last. In a Drawer it sits at the bottom of the screen. */
export function DialogFooter({ children }: { children: ReactNode }) {
  return (
    <div className="-mx-6 mt-auto -mb-5 flex flex-wrap justify-end gap-2 border-t border-border bg-surface px-6 py-3">{children}</div>
  );
}
