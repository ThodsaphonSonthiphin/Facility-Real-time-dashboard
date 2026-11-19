import { useSearchParams } from 'react-router';

/**
 * Tests only whether the URL includes `source=overview` before choosing between the overview drill-through view and the
 * legacy page; strict validation stays inside the drill-through component so invalid URLs never fall back to legacy.
 */
export function useOverviewSource(): boolean {
  const [params] = useSearchParams();
  return params.getAll('source').includes('overview');
}
