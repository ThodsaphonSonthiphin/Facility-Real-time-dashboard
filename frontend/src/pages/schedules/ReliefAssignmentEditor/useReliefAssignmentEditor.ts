import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import type { ReliefCandidate, RowProblem, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import { formatThaiDate } from '../../../shared/utils/thaiTime';
import type { CommandDraft } from '../commandRules';
import { useReliefLoanEditor } from '../ReliefLoanEditor/useReliefLoanEditor';
import type { ScheduleChangeLine } from '../ScheduleReview/type';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { clockSlot } from '../scheduleRules';
import { useGetReliefCandidateQuery } from '../schedulesApi';
import type { ReliefAssignmentEditorProps, ReliefAssignmentRequest } from './type';

export function useReliefAssignmentEditor(props: ReliefAssignmentEditorProps, mode: 'own' | 'loan') {
  const [baseWeek, setBaseWeek] = useState(props.week);
  const today = clockSlot(new Date()).startDate;
  const dates = [...new Set(props.week.destinations?.map((slot) => slot.date))].sort();
  const [date, setDate] = useState(dates.includes(today) ? today : dates[0] ?? '');
  const slots = props.week.destinations?.filter((slot) => slot.date === date) ?? [];
  const [shiftChoice, setShiftChoice] = useState('');
  const shift = slots.find((slot) => slot.shift === shiftChoice)?.shift ?? slots[0]?.shift ?? 'Day';
  const choices = slots.filter((slot) => slot.shift === shift);
  const [area, setArea] = useState('');
  const [personId, setPerson] = useState('');
  const [replacementId, setReplacement] = useState('');
  const [group, setGroup] = useState('');
  const [reason, setReason] = useState('');
  const [attestation, setAttestation] = useState<string>();
  const [reviewed, setReviewed] = useState<{ candidate: ReliefCandidate; lines: ScheduleChangeLine[] }>();
  const [compared, setCompared] = useState<{ week: ScheduleWeek; candidate: ReliefCandidate }>();
  const loan = useReliefLoanEditor();

  const destination = choices.find((slot) => slot.areas.some((option) => String(option.id) === area));
  const areaOption = destination?.areas.find((option) => String(option.id) === area);
  function people(week: ScheduleWeek | undefined) {
    return week?.rows.filter((row) => row.cleanerKind === 'Relief' && row.homeBuildingId
      && row.days.some((day) => day.date === date && day.shift === shift && day.edit?.canChange)) ?? [];
  }
  function replacements(week: ScheduleWeek | undefined) {
    return week?.rows.filter((row) => row.cleanerKind === 'Regular'
      && row.days.some((day) => day.date === date && day.shift === shift && day.edit?.canChange
        && day.area.value === areaOption?.id && areaOption)) ?? [];
  }
  const ownPeople = people(baseWeek);
  const person = ownPeople.find((row) => String(row.userId) === personId);
  const replaced = replacements(baseWeek);
  const replacement = replaced.find((row) => String(row.userId) === replacementId);
  const inherited = replacement?.days.find((day) => day.date === date)?.breakGroup.value;
  const source = mode === 'own' ? person?.homeBuildingId : Number(loan.submitted?.sourceBuildingId);
  const search = mode === 'own' && person ? { ownUserId: person.userId } :
    mode === 'loan' && loan.submitted?.employeeId ? { employeeId: loan.submitted.employeeId } : undefined;
  const lookup = useGetReliefCandidateQuery(source && areaOption && search
    ? { ...search, sourceBuildingId: source, destinationAreaId: areaOption.id, date, shift }
    : skipToken, { refetchOnMountOrArgChange: true });
  const candidate = lookup.error ? undefined : lookup.currentData;
  const userId = candidate?.userId ?? person?.userId;
  const key = `relief:${userId ?? 'draft'}:${date}:${shift}`;
  const requiresAgreement = candidate?.agreementRequired === true;
  const attestationKey = candidate && requiresAgreement
    ? `${key}:${candidate.homeBuildingId}:${candidate.sourceSupervisorId ?? 'vacant'}` : undefined;
  const attested = attestationKey !== undefined && attestation === attestationKey;
  const payload: ReliefAssignmentRequest = {
    userId: userId ?? 0, areaId: areaOption?.id ?? 0, shiftDate: date, shift, replacedUserId: replacement?.userId,
    breakGroup: group ? Number(group) : undefined, sourceBuildingId: source ?? 0,
    sourceSupervisorId: requiresAgreement ? candidate?.sourceSupervisorId : undefined, agreementReported: requiresAgreement && attested,
    reason: reason.trim() || undefined,
  };
  const command: CommandDraft = { kind: 'assign-relief', payload,
    expectedRevisions: candidate?.revision == null ? {} : { [key]: candidate.revision } };
  const problems: RowProblem[] = [];
  function problem(message: string) {
    problems.push({ rowId: key, code: 'row-invalid', message });
  }
  if (areaOption && (person || candidate) && !group && inherited !== 1 && inherited !== 2) problem('เลือกกลุ่มพักครั้งที่ 1');
  if (requiresAgreement && !attested) problem('ยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว');
  if (candidate && !candidate.eligible) {
    for (const conflict of candidate.conflicts) problem(conflictText[conflict] ?? 'คนนี้มีรายการที่ต้องตรวจสอบในกะที่เลือก');
  }
  if (areaOption && date < today && !reason.trim()) problem('ระบุเหตุผลการแก้แผนย้อนหลัง');

  function replacementDay(week: ScheduleWeek | undefined) {
    return replacements(week).find((row) => String(row.userId) === replacementId)?.days.find((day) => day.date === date);
  }

  function hasScope(week: ScheduleWeek | undefined) {
    return !!week?.destinations?.some((slot) => slot.date === date && slot.shift === shift
      && slot.areas.some((option) => option.id === areaOption?.id))
      && (!personId || people(week).some((row) => String(row.userId) === personId && row.homeBuildingId === source))
      && (!replacementId || !!replacementDay(week));
  }

  function matchesCandidate(expected: ReliefCandidate, next: ReliefCandidate | undefined) {
    return !!next && next.eligible === expected.eligible && next.userId === expected.userId && next.revision === expected.revision
      && next.sourceSupervisorId === expected.sourceSupervisorId && next.agreementRequired === expected.agreementRequired;
  }

  function facts(entry: ReliefCandidate | undefined, group: number | null | undefined) {
    if (!entry) return 'ยังไม่ได้ตรวจสอบกะนี้';
    return [
      entry.eligible ? 'ยังไม่มีงานสแปร์ในกะนี้' : 'มีรายการที่ต้องตรวจสอบในกะนี้',
      `รุ่น ${entry.revision ?? '—'}`,
      group && `กลุ่มพักของคนที่แทน ${group}`,
      entry.sourceSupervisorName && `ซุปตึกต้นทาง ${entry.sourceSupervisorName}`,
    ].filter(Boolean).join(' · ');
  }
  const beforeAfter = areaOption ? [{
    id: key,
    label: `${candidate?.displayName ?? person?.displayName ?? 'แม่บ้านสแปร์'} · ${formatThaiDate(date)} · ${shift === 'Day' ? 'กะเช้า' : 'กะดึก'}`,
    before: facts(candidate, inherited),
    after: [
      `ตึก ${destination?.buildingCode} · ${areaOption.label}`,
      replacement ? `แทน ${replacement.displayName}` : 'งานสแปร์ไม่ได้แทนใคร',
      group ? `กลุ่มพัก ${group} ที่เลือก` : inherited ? `ใช้กลุ่มพัก ${inherited} ของคนที่แทน` : 'ยังไม่เลือกกลุ่มพัก',
      candidate && `ตึกต้นทาง ${candidate.homeBuildingCode}`,
      candidate?.sourceSupervisorName && `ซุปตึกต้นทาง ${candidate.sourceSupervisorName}`,
      payload.agreementReported && 'ผู้บันทึกยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว',
      payload.reason && `เหตุผล ${payload.reason}`,
    ].filter(Boolean).join(' · '),
  }] : [];
  const latest = compared && !lookup.isFetching && hasScope(props.latest) && matchesCandidate(compared.candidate, candidate)
    && replacementDay(props.latest)?.breakGroup.value === replacementDay(compared.week)?.breakGroup.value
    ? (reviewed?.lines ?? beforeAfter).map((line) => ({
      ...line, before: facts(compared.candidate, replacementDay(compared.week)?.breakGroup.value),
    }))
    : undefined;
  const editor = useScheduleReview({
    command, beforeAfter: reviewed?.lines ?? beforeAfter, problems, dirty: !!area || !!loan.fields.employeeId || !!loan.fields.sourceBuildingId,
    ready: !!areaOption && !!candidate?.eligible && candidate.revision != null && !lookup.isFetching,
    changing: !!areaOption && (!hasScope(props.latest)
      || replacementDay(props.latest)?.breakGroup.value !== inherited
      || (!!reviewed && !matchesCandidate(reviewed.candidate, candidate))),
    phone: props.phone, onClose: props.onClose,
    verifyRetryScope: async () => {
      const week = await props.reloadEditorWeek();
      if (!hasScope(week) || replacementDay(week)?.breakGroup.value !== inherited || !reviewed) return false;
      return matchesCandidate(reviewed.candidate, await lookup.refetch().unwrap());
    },
  });
  function resetSelection() {
    setPerson('');
    setReplacement('');
    setAttestation(undefined);
    setReviewed(undefined);
    setCompared(undefined);
    loan.clear();
  }
  return {
    ...editor, latest, dates, date, shift, choices, slots, area, personId, ownPeople, replaced, replacementId, inherited,
    group, reason, attested, requiresAgreement, candidate, candidateQuery: lookup, loan, today,
    chooseDate: (value: string) => { setDate(value); setArea(''); resetSelection(); },
    chooseShift: (value: string) => { setShiftChoice(value); setArea(''); resetSelection(); },
    chooseArea: (value: string) => { setArea(value); resetSelection(); },
    choosePerson: (value: string) => { setPerson(value); setAttestation(undefined); setReviewed(undefined); },
    setReplacement, setGroup, setReason,
    setAttested: (checked: boolean) => setAttestation(checked ? attestationKey : undefined),
    changeLoan: (patch: Parameters<typeof loan.change>[0]) => {
      loan.change(patch); setAttestation(undefined); setReviewed(undefined);
    },
    review: () => {
      if (!editor.canReview || !candidate) return;
      setReviewed({ candidate, lines: beforeAfter });
      setCompared(undefined);
      editor.review();
    },
    backToEdit: () => {
      setReviewed(undefined);
      editor.backToEdit();
    },
    compare: async () => {
      setCompared(undefined);
      if (lookup.isUninitialized) {
        props.onCompare();
        return;
      }
      const refreshed = await Promise.all([props.reloadEditorWeek(), lookup.refetch().unwrap()]).catch(() => undefined);
      if (refreshed && hasScope(refreshed[0]) && refreshed[1].userId === userId) {
        setCompared({ week: refreshed[0], candidate: refreshed[1] });
      }
    },
    useLatest: () => {
      if (!latest || !compared) return;
      setBaseWeek(compared.week);
      setReviewed(undefined);
      setCompared(undefined);
      editor.backToEdit();
    },
  };
}

const conflictText: Record<string, string> = {
  'existing-assignment': 'คนนี้มีงานสแปร์ในกะนี้แล้ว ต้องจัดรายการเดิมให้ถูกก่อน',
  'existing-duty': 'คนนี้มีงานในกะนี้แล้ว ต้องจัดรายการเดิมให้ถูกก่อน',
  'on-leave': 'คนนี้มีบันทึกการลาในกะนี้',
};
