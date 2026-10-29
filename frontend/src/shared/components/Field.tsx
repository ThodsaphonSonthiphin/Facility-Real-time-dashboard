import {
  cloneElement,
  useId,
  type InputHTMLAttributes,
  type ReactElement,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react';
import { Check, TriangleAlert } from 'lucide-react';
import { Note } from './Note';
import { inputClass, linkClass, textAreaClass, type InputSize } from './styles';

interface FieldProps {
  label: ReactNode;
  /** A line under the control, outside the label so it is not part of the control's name. */
  help?: ReactNode;
  /**
   * What is wrong with the answer, under the control in red with an icon. The control gets `aria-invalid` and a 2 px
   * red border, and the message is read after its name (`aria-describedby`), as is the help line.
   */
  error?: ReactNode;
  /** One control: a TextInput, Select or TextArea. */
  children: ReactElement<{
    'aria-describedby'?: string;
    'aria-invalid'?: boolean;
  }>;
}

/**
 * A visible label wrapping its control, so the control's name is the label and nothing more (MASTER.md: no
 * placeholder-only label); the help line and the error are linked with `aria-describedby`, as today's pages do. The
 * label turns grey when its control cannot be used (the design file's Form).
 */
export function Field({ label, help, error, children }: FieldProps) {
  const helpId = useId();
  const errorId = useId();
  const describedBy = [children.props['aria-describedby'], error ? errorId : null, help ? helpId : null]
    .filter(Boolean)
    .join(' ');
  const control =
    help || error
      ? cloneElement(children, {
          'aria-describedby': describedBy,
          ...(error ? { 'aria-invalid': true } : {}),
        })
      : children;
  return (
    <div className="flex flex-col gap-1">
      <label className="flex flex-col gap-1">
        <span className="font-semibold has-disabled:text-text-2">{label}</span>
        {control}
      </label>
      {error && (
        <span id={errorId} className="flex items-center gap-1 text-xs font-semibold text-rework">
          <TriangleAlert aria-hidden="true" className="size-4 shrink-0" />
          {error}
        </span>
      )}
      {help && (
        <span id={helpId} className="text-xs text-text-2">
          {help}
        </span>
      )}
    </div>
  );
}

/**
 * The red box at the top of a form with more than one wrong answer (the design file's Form): one link per answer, to
 * the control's `id`, so the keyboard goes straight to it. Announced at once, like every error note.
 */
export function ErrorSummary({
  title,
  errors,
}: {
  title: ReactNode;
  errors: readonly { fieldId: string; message: ReactNode }[];
}) {
  return (
    <Note tone="error" title={title}>
      <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
        {errors.map((one) => (
          <li key={one.fieldId}>
            <a className={linkClass} href={`#${one.fieldId}`}>
              {one.message}
            </a>
          </li>
        ))}
      </ul>
    </Note>
  );
}

type Sized<T> = Omit<T, 'size'> & { size?: InputSize };

/**
 * A bordered group of controls with its legend, for answers that belong together (D10's shift pattern, D4's round
 * windows). The controls inside lay themselves out; `disabled` reaches every control in the group.
 */
export function FieldGroup({
  legend,
  disabled = false,
  children,
}: {
  legend: ReactNode;
  disabled?: boolean;
  children: ReactNode;
}) {
  return (
    <fieldset className="flex flex-col gap-2 rounded-lg border border-border p-2.5" disabled={disabled}>
      <legend className="px-1 font-semibold">{legend}</legend>
      {children}
    </fieldset>
  );
}

export function TextInput({ size, className, ...rest }: Sized<InputHTMLAttributes<HTMLInputElement>>) {
  return <input className={className ? `${inputClass(size)} ${className}` : inputClass(size)} {...rest} />;
}

export function Select({ size, className, ...rest }: Sized<SelectHTMLAttributes<HTMLSelectElement>>) {
  return <select className={className ? `${inputClass(size)} ${className}` : inputClass(size)} {...rest} />;
}

export function TextArea({ size, className, ...rest }: Sized<TextareaHTMLAttributes<HTMLTextAreaElement>>) {
  return <textarea className={className ? `${textAreaClass(size)} ${className}` : textAreaClass(size)} {...rest} />;
}

interface ChoiceProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'className'> {
  children: ReactNode;
}

/**
 * One answer of a radio group drawn as a large tile (the scan page's เรียบร้อย / พบปัญหา): 112 px, and the chosen one
 * blue with a 3 px frame, never red, whatever the answer means (the design file's Submit and Inspect). The 2 px margin of
 * a tile not chosen makes up for its thinner frame, so choosing one moves nothing. The radio stays a real radio inside
 * the label, so its role and name do not change.
 */
export function ChoiceTile({ children, ...input }: ChoiceProps) {
  return (
    <label
      className={
        'm-0.5 flex min-h-28 cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border border-border-control ' +
        'bg-surface p-3 text-center text-xl font-bold text-text has-checked:m-0 has-checked:border-3 ' +
        'has-checked:border-accent has-checked:bg-accent-subtle has-checked:text-accent-hover ' +
        'has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-accent ' +
        'has-disabled:cursor-not-allowed has-disabled:opacity-50'
      }
    >
      <input type="radio" className="sr-only" {...input} />
      {children}
    </label>
  );
}

/**
 * A checkbox drawn as a 48 px chip (the scan page's issue tags): the chosen one blue-tinted with a 2 px frame and a tick
 * before its words (the design file's Submit). The 1 px margin makes up for the thinner frame when it is not chosen.
 */
export function ToggleChip({ children, ...input }: ChoiceProps) {
  return (
    <label
      className={
        'group m-px inline-flex min-h-12 cursor-pointer items-center gap-1.5 rounded-md border border-border-control ' +
        'bg-surface px-4 text-base font-semibold text-text has-checked:m-0 has-checked:border-2 has-checked:border-accent ' +
        'has-checked:bg-accent-subtle has-checked:text-accent-hover has-focus-visible:outline-3 ' +
        'has-focus-visible:outline-offset-2 has-focus-visible:outline-accent has-disabled:cursor-not-allowed ' +
        'has-disabled:opacity-50'
      }
    >
      <input type="checkbox" className="sr-only" {...input} />
      <Check aria-hidden="true" className="hidden size-4 shrink-0 group-has-checked:block" />
      {children}
    </label>
  );
}
