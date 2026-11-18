import type { CommandResult, ScheduleCommandEnvelope } from '../../shared/api/scheduleTypes';

export type CommandUi = 'editing' | 'reviewing' | 'sending' | 'uncertain' | 'committed' | 'conflict';
export type CommandOutcome = 'committed' | 'stale' | 'network-error' | 'lookup-failed';
export type CommandDraft = Pick<ScheduleCommandEnvelope, 'kind' | 'payload' | 'expectedRevisions'>;

export function resolveCommandUi(current: CommandUi, outcome: CommandOutcome): CommandUi {
  if (outcome === 'committed') return 'committed';
  if (outcome === 'stale') return 'conflict';
  if (outcome === 'network-error' || outcome === 'lookup-failed') return 'uncertain';
  return current;
}

export function resultFromError(error: unknown, commandId: string): CommandResult | undefined {
  if (typeof error !== 'object' || error === null || !('data' in error)) return;
  const result = error.data as Partial<CommandResult> | undefined;
  if (result?.commandId === commandId && result.outcome && Array.isArray(result.problems)) {
    return result as CommandResult;
  }
}

export function offsetDate(date: string, days: number): string {
  return new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000).toISOString().slice(0, 10);
}

export const weekdays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;
export const weekdayOf = (date: string) => weekdays[new Date(`${date}T00:00:00Z`).getUTCDay()];
