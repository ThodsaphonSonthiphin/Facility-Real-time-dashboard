import { useSearchParams } from 'react-router';

/**
 * Tests only `params.get('source') === 'overview'` before choosing between the overview drill-through view and the
 * legacy page; strict validation stays inside the drill-through component so invalid URLs never fall back to legacy.
 */
export function useOverviewSource(): boolean {
  const [params] = useSearchParams();
  return params.get('source') === 'overview';
}
