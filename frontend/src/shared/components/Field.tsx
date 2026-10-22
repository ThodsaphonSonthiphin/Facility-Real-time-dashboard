import {
  cloneElement,
  useId,
  type InputHTMLAttributes,
  type ReactElement,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react';
import { inputClass, textAreaClass, type InputSize } from './styles';

interface FieldProps {
  label: ReactNode;
  /** A line under the control, outside the label so it is not part of the control's name. */
  help?: ReactNode;
  /** One control: a TextInput, Select or TextArea. */
  children: ReactElement<{ 'aria-describedby'?: string }>;
}

/**
 * A visible label wrapping its control, so the control's name is the label and nothing more (MASTER.md: no
 * placeholder-only label); the help line is linked with `aria-describedby`, as today's pages do.
 */
export function Field({ label, help, children }: FieldProps) {
  const helpId = useId();
  const control = help
    ? cloneElement(children, {
        'aria-describedby': [children.props['aria-describedby'], helpId].filter(Boolean).join(' '),
      })
    : children;
  return (
    <div className="flex flex-col gap-1">
      <label className="flex flex-col gap-1">
        <span className="font-semibold">{label}</span>
        {control}
      </label>
      {help && (
        <span id={helpId} className="text-muted">
          {help}
        </span>
      )}
    </div>
  );
}

type Sized<T> = Omit<T, 'size'> & { size?: InputSize };

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
 * One answer of a radio group drawn as a large tile (the scan page's เรียบร้อย / พบปัญหา, at least 96 px high). The
 * radio stays a real radio inside the label, so its role and name do not change.
 */
export function ChoiceTile({ children, ...input }: ChoiceProps) {
  return (
    <label
      className={
        'flex min-h-24 cursor-pointer flex-col items-center justify-center gap-1.5 rounded border-2 border-input-border ' +
        'bg-surface p-3 text-center text-lg font-bold has-checked:border-primary has-checked:bg-info-bg ' +
        'has-checked:text-info-text has-focus-visible:outline-3 has-focus-visible:outline-offset-2 ' +
        'has-focus-visible:outline-primary has-disabled:cursor-not-allowed has-disabled:opacity-50'
      }
    >
      <input type="radio" className="sr-only" {...input} />
      {children}
    </label>
  );
}

/** A checkbox drawn as a 48 px chip that turns blue when on (the scan page's issue tags, as in the mockup). */
export function ToggleChip({ children, ...input }: ChoiceProps) {
  return (
    <label
      className={
        'inline-flex min-h-12 cursor-pointer items-center gap-1.5 rounded-full border border-input-border bg-surface ' +
        'px-4 text-base has-checked:border-primary has-checked:bg-primary has-checked:font-semibold ' +
        'has-checked:text-on-primary has-focus-visible:outline-3 has-focus-visible:outline-offset-2 ' +
        'has-focus-visible:outline-primary has-disabled:cursor-not-allowed has-disabled:opacity-50'
      }
    >
      <input type="checkbox" className="sr-only" {...input} />
      {children}
    </label>
  );
}
