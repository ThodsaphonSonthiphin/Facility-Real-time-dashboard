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
        if (isMounted) setPoint(data);
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

  const submitScan = useCallback(async (): Promise<boolean> => {
    if (!userId || !qrToken) {
      setSubmitError('ไม่พบข้อมูลผู้ใช้หรือรหัส QR');
      return false;
    }

    setIsSubmitting(true);
    setSubmitError(null);

    try {
      const res = await createScanRecordApi({
        qrToken,
        userId,
        status,
        issueTags: status === 'Issue' ? selectedTags : undefined,
        notes: status === 'Issue' && notes.trim() ? notes.trim() : undefined
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
    resetForm
  };
}
