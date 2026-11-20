import type { ReactNode } from 'react';

export interface AdminPageHeaderProps {
  /** Main page heading (`h1`). */
  title: ReactNode;
  /** Prominent title for a landing page. */
  titleSize?: 'standard' | 'prominent';
  /** Optional subtitle line under the heading. */
  subtitle?: ReactNode;
  /** Optional inline badge beside the title (e.g. "อ่านอย่างเดียว"). */
  badge?: ReactNode;
  /** Optional live status badge or action buttons aligned to the right. */
  actions?: ReactNode;
  /** Optional back navigation link rendered above the title. */
  back?: ReactNode;
  /** Additional classes for the `<header>` element (e.g. `print:hidden`). */
  className?: string;
}
