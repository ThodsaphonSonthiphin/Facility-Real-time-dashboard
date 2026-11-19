import type { ReactNode } from 'react';

/** Props for the Enterprise Light card/section wrapper. */
export interface SurfacePanelProps {
  title?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  bodyClassName?: string;
}

/**
 * Structured Enterprise Light card/section wrapper with optional header bar.
 */
export function SurfacePanel({ title, actions, children, className, bodyClassName }: SurfacePanelProps) {
  return (
    <section
      className={`overflow-hidden rounded-xl border border-border bg-surface${className ? ` ${className}` : ''}`}
    >
      {(title || actions) && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3.5">
          {title && <h2 className="text-base font-bold text-text">{title}</h2>}
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className={bodyClassName}>{children}</div>
    </section>
  );
}

/** Props for the responsive filter and action toolbar wrapper. */
export interface FilterToolbarProps {
  label: string;
  children: ReactNode;
  actions?: ReactNode;
  align?: 'center' | 'end';
  className?: string;
}

const toolbarAlign = {
  center: 'items-center',
  end: 'items-end',
} as const;

/**
 * Responsive flex/wrap toolbar wrapper for Admin filter controls and secondary actions.
 */
export function FilterToolbar({ label, children, actions, align = 'center', className }: FilterToolbarProps) {
  const alignClass = toolbarAlign[align];
  return (
    <div
      role="region"
      aria-label={label}
      className={`flex flex-wrap ${alignClass} ${actions ? 'justify-between' : 'justify-start'} gap-3 rounded-lg border border-border bg-surface px-4 py-3${className ? ` ${className}` : ''}`}
    >
      {actions ? (
        <>
          <div className={`flex flex-wrap ${alignClass} gap-2`}>{children}</div>
          <div className="flex flex-wrap items-center gap-2">{actions}</div>
        </>
      ) : (
        children
      )}
    </div>
  );
}
