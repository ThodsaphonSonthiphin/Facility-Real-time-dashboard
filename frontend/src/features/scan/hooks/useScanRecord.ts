import { useState, useEffect, useCallback } from 'react';
import type { ServicePointStatus, ScanStatus, CreateScanResponse } from '../../../types';
import { getServicePointByTokenApi, createScanRecordApi } from '../../../services/api';

export function useScanRecord(qrToken: string, userId: number | undefined) {
  const [point, setPoint] = useState<ServicePointStatus | null>(null);
  const [isLoadingPoint, setIsLoadingPoint] = useState<boolean>(true);
  const [pointError, setPointError] = useState<string | null>(null);

  const [status, setStatus] = useState<ScanStatus>('Normal');
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [notes, setNotes] = useState<string>('');

  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitResult, setSubmitResult] = useState<CreateScanResponse | null>(null);

  // Fetch Service Point details by QR Token
  useEffect(() => {
    if (!qrToken) return;

    let isMounted = true;
    setIsLoadingPoint(true);
    setPointError(null);

    getServicePointByTokenApi(qrToken)
      .then((data) => {
        if (isMounted) {
          setPoint(data);
          // If point currently has an active issue, pre-populate its tags and notes
          if (data.currentStatus === 'Issue' || data.lastScanStatus === 'Issue') {
            if (data.lastIssueTags && data.lastIssueTags.length > 0) {
              setSelectedTags(data.lastIssueTags);
            }
            if (data.lastNotes) {
              setNotes(data.lastNotes);
            }
            setStatus('Issue');
          }
        }
      })
      .catch((err) => {
        if (isMounted) setPointError(err.message || 'ไม่พบจุดบริการนี้');
      })
      .finally(() => {
        if (isMounted) setIsLoadingPoint(false);
      });

    return () => {
      isMounted = false;
    };
  }, [qrToken]);

  const toggleTag = useCallback((tagKey: string) => {
    setSelectedTags((prev) =>
      prev.includes(tagKey) ? prev.filter((t) => t !== tagKey) : [...prev, tagKey]
    );
  }, []);

  const submitScan = useCallback(async (overrideStatus?: ScanStatus): Promise<boolean> => {
    if (!userId || !qrToken) {
      setSubmitError('ไม่พบข้อมูลผู้ใช้หรือรหัส QR');
      return false;
    }

    const targetStatus = overrideStatus || status;
    setIsSubmitting(true);
    setSubmitError(null);

    try {
      const res = await createScanRecordApi({
        qrToken,
        userId,
        status: targetStatus,
        issueTags: targetStatus === 'Issue' && selectedTags.length > 0 ? selectedTags : undefined,
        notes: targetStatus === 'Issue' && notes.trim() ? notes.trim() : undefined
      });

      setSubmitResult(res);
      return true;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'บันทึกไม่สำเร็จ';
      setSubmitError(msg);
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }, [qrToken, userId, status, selectedTags, notes]);

  const resetForm = useCallback(() => {
    setStatus('Normal');
    setSelectedTags([]);
    setNotes('');
    setSubmitResult(null);
    setSubmitError(null);
  }, []);

  return {
    point,
    isLoadingPoint,
    pointError,
    status,
    setStatus,
    selectedTags,
    toggleTag,
    notes,
    setNotes,
    isSubmitting,
    submitError,
    submitResult,
    submitScan,
    resetForm,
    existingIssueTags: (point?.currentStatus === 'Issue' || point?.lastScanStatus === 'Issue') ? (point?.lastIssueTags || []) : []
  };
}
