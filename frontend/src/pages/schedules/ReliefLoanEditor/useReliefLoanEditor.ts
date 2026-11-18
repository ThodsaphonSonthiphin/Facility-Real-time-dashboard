import { useState } from 'react';
import type { ReliefLoanSearch } from './type';

export function useReliefLoanEditor() {
  const [fields, setFields] = useState<ReliefLoanSearch>({ employeeId: '', sourceBuildingId: '' });
  const [submitted, setSubmitted] = useState<ReliefLoanSearch>();
  return {
    fields, submitted,
    change: (patch: Partial<ReliefLoanSearch>) => {
      setFields((current) => ({ ...current, ...patch }));
      setSubmitted(undefined);
    },
    search: () => setSubmitted({ ...fields, employeeId: fields.employeeId.trim() }),
    clear: () => setSubmitted(undefined),
  };
}
