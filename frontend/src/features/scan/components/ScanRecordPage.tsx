import React, { useState } from 'react';
import { 
  MapPin, 
  Clock, 
  CheckCircle2, 
  AlertTriangle, 
  UserCheck, 
  LogOut, 
  AlertCircle, 
  ChevronDown,
  ChevronUp
} from 'lucide-react';
import { useScanRecord } from '../hooks/useScanRecord';
import { IssueTagSelector } from './IssueTagSelector';
import type { UserSession } from '../../../types';

interface ScanRecordPageProps {
  qrToken: string;
  currentUser: UserSession;
  onLogout: () => void;
  onOpenDashboard?: () => void;
}

export const ScanRecordPage: React.FC<ScanRecordPageProps> = ({
  qrToken,
  currentUser,
  onLogout,
  onOpenDashboard
}) => {
  const {
    point,
    isLoadingPoint,
    pointError,
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
  } = useScanRecord(qrToken, currentUser.id);

  const [showIssueForm, setShowIssueForm] = useState(false);

  // Fast 1-Tap Normal Submission
  const handleFastNormalSubmit = async () => {
    setStatus('Normal');
    await submitScan('Normal');
  };

  const handleIssueSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatus('Issue');
    await submitScan('Issue');
  };

  if (isLoadingPoint) {
    return (
      <div style={styles.centerContainer}>
        <div style={styles.loadingSpinner} />
        <p style={{ color: '#94a3b8', marginTop: '16px' }}>กำลังโหลดข้อมูลจุดบริการ...</p>
      </div>
    );
  }

  if (pointError || !point) {
    return (
      <div style={styles.centerContainer}>
        <div style={styles.errorBox}>
          <AlertCircle size={32} color="#ef4444" />
          <h2 style={{ fontSize: '18px', color: '#f8fafc', marginTop: '12px' }}>ไม่พบจุดบริการ</h2>
          <p style={{ fontSize: '14px', color: '#94a3b8', marginTop: '6px' }}>{pointError || 'รหัส QR ไม่ถูกต้อง'}</p>
          {onOpenDashboard && (
            <button onClick={onOpenDashboard} style={styles.backButton}>
              ไปยังหน้า Dashboard
            </button>
          )}
        </div>
      </div>
    );
  }

  // Success Screen
  if (submitResult) {
    const isNormal = submitResult.newPointStatus === 'Normal';
    return (
      <div style={styles.container}>
        <div style={styles.successCard}>
          <div style={{
            ...styles.successIconBadge,
            background: isNormal ? 'rgba(34, 197, 94, 0.2)' : 'rgba(239, 68, 68, 0.2)',
            borderColor: isNormal ? '#22c55e' : '#ef4444'
          }}>
            {isNormal ? (
              <CheckCircle2 size={44} color="#22c55e" />
            ) : (
              <AlertTriangle size={44} color="#ef4444" />
            )}
          </div>

          <h2 style={styles.successTitle}>
            {isNormal ? 'บันทึกงานเรียบร้อยแล้ว!' : 'รายงานปัญหาสำเร็จแล้ว!'}
          </h2>
          <p style={styles.successSubtitle}>
            ข้อมูลถูกส่งไปยังหน้า Dashboard แบบเรียลไทม์ทันที
          </p>

          <div style={styles.summaryBox}>
            <div style={styles.summaryRow}>
              <span style={styles.summaryLabel}>จุดบริการ:</span>
              <span style={styles.summaryValue}>{point.name}</span>
            </div>
            <div style={styles.summaryRow}>
              <span style={styles.summaryLabel}>สถานะที่บันทึก:</span>
              <span style={{
                fontWeight: 700,
                color: isNormal ? '#22c55e' : '#ef4444'
              }}>
                {isNormal ? 'ปกติ (เรียบร้อย)' : 'พบปัญหา / แจ้งเตือน'}
              </span>
            </div>
            <div style={styles.summaryRow}>
              <span style={styles.summaryLabel}>ผู้บันทึก:</span>
              <span style={styles.summaryValue}>{currentUser.fullName}</span>
            </div>
          </div>

          <button
            onClick={resetForm}
            style={styles.doneButton}
          >
            บันทึกใหม่อีกครั้ง
          </button>

          {onOpenDashboard && (
            <button
              onClick={onOpenDashboard}
              style={styles.dashboardLinkButton}
            >
              ดูหน้าจอ Dashboard
            </button>
          )}
        </div>
      </div>
    );
  }

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        {/* Cleaner User Header */}
        <div style={styles.cleanerHeader}>
          <div style={styles.cleanerInfo}>
            <UserCheck size={18} color="#38bdf8" />
            <span style={styles.cleanerName}>{currentUser.fullName}</span>
          </div>
          <button onClick={onLogout} style={styles.logoutBtn} title="ออกจากระบบ">
            <LogOut size={16} />
            <span>ออก</span>
          </button>
        </div>

        {/* Location Info Banner */}
        <div style={styles.locationCard}>
          <div style={styles.locationHeader}>
            <span style={styles.pointBadge}>จุดบริการที่สแกน</span>
            <div style={styles.intervalBadge}>
              <Clock size={12} />
              <span>รอบทุก {point.cleaningIntervalMinutes} นาที</span>
            </div>
          </div>

          <h1 style={styles.pointTitle}>{point.name}</h1>
          <div style={styles.locationDetail}>
            <MapPin size={14} color="#94a3b8" />
            <span>{point.location}</span>
          </div>

          {point.lastScannedAt && (
            <div style={styles.lastScanNotice}>
              <span>ทำความสะอาดล่าสุดเมื่อ: </span>
              <strong style={{ color: '#cbd5e1' }}>
                {new Date(point.lastScannedAt).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })} น.
              </strong>
              {point.lastCleanerName && <span> โดย {point.lastCleanerName}</span>}
            </div>
          )}
        </div>

        {submitError && (
          <div style={styles.errorAlert}>
            <AlertCircle size={16} color="#ef4444" />
            <span>{submitError}</span>
          </div>
        )}

        {/* Fast 1-Tap Action: Normal */}
        {!showIssueForm ? (
          <div style={styles.actionContainer}>
            <button
              id="tap-normal-btn"
              type="button"
              disabled={isSubmitting}
              onClick={handleFastNormalSubmit}
              style={{
                ...styles.fastNormalBtn,
                opacity: isSubmitting ? 0.7 : 1
              }}
            >
              <div style={styles.btnInnerContent}>
                <CheckCircle2 size={26} color="#ffffff" />
                <div style={styles.btnTextGroup}>
                  <span style={styles.btnMainText}>✓ ทำความสะอาดเรียบร้อย</span>
                  <span style={styles.btnSubText}>แตะปุ่มนี้เพื่อบันทึกสถานะปกติทันที (1-Tap)</span>
                </div>
              </div>
            </button>

            <button
              id="toggle-issue-btn"
              type="button"
              onClick={() => setShowIssueForm(true)}
              style={styles.toggleIssueBtn}
            >
              <AlertTriangle size={18} color="#f87171" />
              <span>พบปัญหา / อุปกรณ์ชำรุด (แตะเพื่อระบุปัญหา)</span>
              <ChevronDown size={18} />
            </button>
          </div>
        ) : (
          /* Issue Reporting Form */
          <form onSubmit={handleIssueSubmit} style={styles.issueForm}>
            <div style={styles.issueHeader}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <AlertTriangle size={18} color="#ef4444" />
                <span style={styles.issueFormTitle}>รายงานปัญหาที่จุดบริการ</span>
              </div>
              <button
                type="button"
                onClick={() => setShowIssueForm(false)}
                style={styles.closeIssueBtn}
              >
                <ChevronUp size={18} />
                <span>ซ่อน</span>
              </button>
            </div>

            <IssueTagSelector
              selectedTags={selectedTags}
              onToggleTag={toggleTag}
            />

            <div style={styles.notesGroup}>
              <label style={styles.notesLabel}>หมายเหตุเพิ่มเติม (ถ้ามี):</label>
              <textarea
                id="issue-notes-input"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="ระบุรายละเอียด เช่น น้ำรั่วจากก๊อกอ่างล้างมือตัวที่ 2"
                rows={3}
                style={styles.notesTextarea}
              />
            </div>

            <div style={styles.issueButtonGroup}>
              <button
                id="submit-issue-btn"
                type="submit"
                disabled={isSubmitting}
                style={{
                  ...styles.submitIssueBtn,
                  opacity: isSubmitting ? 0.7 : 1
                }}
              >
                <AlertTriangle size={18} />
                <span>{isSubmitting ? 'กำลังส่งข้อมูล...' : 'ยืนยันส่งรายงานปัญหา'}</span>
              </button>

              <button
                type="button"
                onClick={() => setShowIssueForm(false)}
                style={styles.cancelIssueBtn}
              >
                ยกเลิก
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100vh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '16px'
  },
  card: {
    width: '100%',
    maxWidth: '460px',
    background: 'rgba(30, 41, 59, 0.85)',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    borderRadius: '24px',
    padding: '24px 20px',
    boxShadow: '0 20px 40px rgba(0, 0, 0, 0.5)',
    backdropFilter: 'blur(16px)'
  },
  cleanerHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingBottom: '16px',
    marginBottom: '16px',
    borderBottom: '1px solid rgba(255, 255, 255, 0.08)'
  },
  cleanerInfo: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px'
  },
  cleanerName: {
    fontSize: '14px',
    fontWeight: 600,
    color: '#38bdf8'
  },
  logoutBtn: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
    background: 'rgba(255, 255, 255, 0.06)',
    border: '1px solid rgba(255, 255, 255, 0.08)',
    borderRadius: '8px',
    padding: '6px 10px',
    color: '#94a3b8',
    fontSize: '12px',
    cursor: 'pointer'
  },
  locationCard: {
    background: 'linear-gradient(135deg, rgba(15, 23, 42, 0.7) 0%, rgba(30, 41, 59, 0.5) 100%)',
    border: '1px solid rgba(56, 189, 248, 0.2)',
    borderRadius: '18px',
    padding: '18px 16px',
    marginBottom: '20px'
  },
  locationHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '10px'
  },
  pointBadge: {
    fontSize: '11px',
    fontWeight: 700,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
    color: '#38bdf8',
    background: 'rgba(56, 189, 248, 0.12)',
    padding: '3px 8px',
    borderRadius: '6px'
  },
  intervalBadge: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
    fontSize: '12px',
    color: '#94a3b8'
  },
  pointTitle: {
    fontSize: '20px',
    fontWeight: 700,
    color: '#f8fafc',
    marginBottom: '6px'
  },
  locationDetail: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: '13px',
    color: '#94a3b8'
  },
  lastScanNotice: {
    marginTop: '12px',
    paddingTop: '10px',
    borderTop: '1px dashed rgba(255, 255, 255, 0.08)',
    fontSize: '12px',
    color: '#64748b'
  },
  actionContainer: {
    display: 'flex',
    flexDirection: 'column',
    gap: '14px'
  },
  fastNormalBtn: {
    padding: '20px 16px',
    borderRadius: '18px',
    border: 'none',
    background: 'linear-gradient(135deg, #16a34a 0%, #15803d 100%)',
    boxShadow: '0 10px 25px -5px rgba(22, 163, 74, 0.5)',
    color: '#ffffff',
    cursor: 'pointer',
    transition: 'transform 0.1s, box-shadow 0.2s',
    textAlign: 'left'
  },
  btnInnerContent: {
    display: 'flex',
    alignItems: 'center',
    gap: '14px'
  },
  btnTextGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px'
  },
  btnMainText: {
    fontSize: '18px',
    fontWeight: 700,
    letterSpacing: '0.01em'
  },
  btnSubText: {
    fontSize: '12px',
    opacity: 0.9
  },
  toggleIssueBtn: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '14px 16px',
    borderRadius: '14px',
    border: '1px solid rgba(239, 68, 68, 0.25)',
    background: 'rgba(239, 68, 68, 0.08)',
    color: '#fca5a5',
    fontSize: '14px',
    fontWeight: 600,
    cursor: 'pointer',
    transition: 'background 0.2s'
  },
  issueForm: {
    display: 'flex',
    flexDirection: 'column',
    gap: '14px',
    background: 'rgba(15, 23, 42, 0.5)',
    border: '1px solid rgba(239, 68, 68, 0.3)',
    borderRadius: '18px',
    padding: '18px 16px'
  },
  issueHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between'
  },
  issueFormTitle: {
    fontSize: '15px',
    fontWeight: 700,
    color: '#fca5a5'
  },
  closeIssueBtn: {
    display: 'flex',
    alignItems: 'center',
    gap: '2px',
    background: 'transparent',
    border: 'none',
    color: '#94a3b8',
    fontSize: '12px',
    cursor: 'pointer'
  },
  notesGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px'
  },
  notesLabel: {
    fontSize: '13px',
    fontWeight: 600,
    color: '#cbd5e1'
  },
  notesTextarea: {
    width: '100%',
    padding: '10px 12px',
    borderRadius: '10px',
    background: 'rgba(15, 23, 42, 0.8)',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    color: '#f8fafc',
    fontSize: '13px',
    outline: 'none',
    resize: 'vertical'
  },
  issueButtonGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
    marginTop: '6px'
  },
  submitIssueBtn: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '8px',
    padding: '14px',
    borderRadius: '12px',
    border: 'none',
    background: 'linear-gradient(135deg, #dc2626 0%, #b91c1c 100%)',
    boxShadow: '0 8px 20px -4px rgba(220, 38, 38, 0.5)',
    color: '#ffffff',
    fontSize: '15px',
    fontWeight: 700,
    cursor: 'pointer'
  },
  cancelIssueBtn: {
    padding: '10px',
    borderRadius: '10px',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    background: 'transparent',
    color: '#94a3b8',
    fontSize: '13px',
    cursor: 'pointer'
  },
  centerContainer: {
    minHeight: '100vh',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '20px'
  },
  loadingSpinner: {
    width: '40px',
    height: '40px',
    borderRadius: '50%',
    border: '3px solid rgba(56, 189, 248, 0.2)',
    borderTopColor: '#38bdf8',
    animation: 'spin 0.8s linear infinite'
  },
  errorBox: {
    maxWidth: '380px',
    textAlign: 'center',
    background: 'rgba(30, 41, 59, 0.8)',
    border: '1px solid rgba(239, 68, 68, 0.3)',
    borderRadius: '20px',
    padding: '30px 20px'
  },
  backButton: {
    marginTop: '20px',
    padding: '10px 18px',
    background: 'rgba(255, 255, 255, 0.1)',
    border: '1px solid rgba(255, 255, 255, 0.15)',
    borderRadius: '10px',
    color: '#f8fafc',
    fontSize: '14px',
    cursor: 'pointer'
  },
  successCard: {
    width: '100%',
    maxWidth: '420px',
    textAlign: 'center',
    background: 'rgba(30, 41, 59, 0.9)',
    border: '1px solid rgba(255, 255, 255, 0.12)',
    borderRadius: '24px',
    padding: '36px 24px',
    boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.6)'
  },
  successIconBadge: {
    width: '80px',
    height: '80px',
    borderRadius: '50%',
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    border: '2px solid',
    marginBottom: '20px'
  },
  successTitle: {
    fontSize: '22px',
    fontWeight: 700,
    color: '#f8fafc',
    marginBottom: '8px'
  },
  successSubtitle: {
    fontSize: '14px',
    color: '#94a3b8',
    marginBottom: '24px'
  },
  summaryBox: {
    background: 'rgba(15, 23, 42, 0.6)',
    borderRadius: '14px',
    padding: '14px 16px',
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
    marginBottom: '24px',
    textAlign: 'left'
  },
  summaryRow: {
    display: 'flex',
    justifyContent: 'space-between',
    fontSize: '13px'
  },
  summaryLabel: {
    color: '#94a3b8'
  },
  summaryValue: {
    color: '#f8fafc',
    fontWeight: 600
  },
  doneButton: {
    width: '100%',
    padding: '14px',
    borderRadius: '12px',
    border: 'none',
    background: '#0284c7',
    color: '#ffffff',
    fontSize: '15px',
    fontWeight: 700,
    cursor: 'pointer'
  },
  dashboardLinkButton: {
    width: '100%',
    padding: '12px',
    borderRadius: '12px',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    background: 'transparent',
    color: '#94a3b8',
    fontSize: '14px',
    marginTop: '10px',
    cursor: 'pointer'
  },
  errorAlert: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '10px 12px',
    background: 'rgba(239, 68, 68, 0.15)',
    border: '1px solid rgba(239, 68, 68, 0.3)',
    borderRadius: '10px',
    color: '#fca5a5',
    fontSize: '13px',
    marginBottom: '14px'
  }
};
