import type { AdminPageHeaderProps } from './type';

/**
 * Standardized header across all Admin pages (Heuristic #4: Consistency & Standards).
 * Keeps typography, subtitle rhythm, and right-aligned actions uniform on desktop and mobile.
 */
export function AdminPageHeader({
  title, titleSize = 'standard', subtitle, badge, actions, back, className,
}: AdminPageHeaderProps) {
  return (
    <header className={`flex flex-col gap-1.5${className ? ` ${className}` : ''}`}>
      {back && <div className="flex items-center">{back}</div>}
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-3">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className={`text-2xl font-bold text-text ${titleSize === 'prominent' ? 'sm:text-3xl' : ''}`}>
              {title}
            </h1>
            {badge}
          </div>
          {subtitle !== undefined && (
            <p className="min-h-5 text-sm text-text-2">{subtitle}</p>
          )}
        </div>
        {actions && (
          <div className="flex flex-wrap items-center gap-2">{actions}</div>
        )}
      </div>
    </header>
  );
}
