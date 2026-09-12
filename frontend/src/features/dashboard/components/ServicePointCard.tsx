import React, { useState } from 'react';
import { MapPin, Clock, User, AlertTriangle, QrCode, X } from 'lucide-react';
import type { ServicePointStatus, PointStatus } from '../../../types';

interface ServicePointCardProps {
  point: ServicePointStatus;
  isHighlighted?: boolean;
  onOpenScanner?: (qrToken: string) => void;
}

export const ServicePointCard: React.FC<ServicePointCardProps> = ({
  point,
  isHighlighted,
  onOpenScanner
}) => {
  const [showQrModal, setShowQrModal] = useState(false);
  const scanUrl = `http://10.249.194.205:5173/scan/${point.qrToken}`;
  const getStatusBadge = (status: PointStatus) => {
    switch (status) {
      case 'Normal':
        return (
          <span className="badge-pill badge-normal">
            <span className="indicator-dot dot-normal" />
            ปกติ
          </span>
        );
      case 'Overdue':
        return (
          <span className="badge-pill badge-overdue">
            <span className="indicator-dot dot-overdue" />
            เลยรอบกำหนด
          </span>
        );
      case 'Issue':
        return (
          <span className="badge-pill badge-issue">
            <span className="indicator-dot dot-issue" />
            พบปัญหา
          </span>
        );
      case 'OffHours':
        return (
          <span className="badge-pill badge-offhours">
            <span className="indicator-dot dot-offhours" />
            นอกเวลาทำการ
          </span>
        );
    }
  };

  const formatElapsed = (minutes: number, lastScannedAt: string | null) => {
    if (!lastScannedAt || minutes >= 999) {
      return 'ยังไม่มีข้อมูลสแกน';
    }
    if (minutes < 1) {
      return 'เพิ่งทำความสะอาด';
    }
    if (minutes < 60) {
      return `${minutes} นาทีที่แล้ว`;
    }
    const hours = Math.floor(minutes / 60);
    return `${hours} ชม. ที่แล้ว`;
  };

  return (
    <div
      style={{
        ...styles.card,
        borderColor: isHighlighted ? '#38bdf8' : getBorderColor(point.currentStatus),
        boxShadow: isHighlighted
          ? '0 0 25px rgba(56, 189, 248, 0.4)'
          : getShadow(point.currentStatus)
      }}
      className={isHighlighted ? 'flash-animate' : ''}
    >
      {/* Card Header: Status & Interval */}
      <div style={styles.cardHeader}>
        {getStatusBadge(point.currentStatus)}
        <div style={styles.intervalTag}>
          <Clock size={12} />
          <span>รอบ {point.cleaningIntervalMinutes} น.</span>
        </div>
      </div>

      {/* Point Name & Location */}
      <h3 style={styles.title}>{point.name}</h3>
      <div style={styles.locationRow}>
        <MapPin size={14} color="#94a3b8" />
        <span>{point.location}</span>
      </div>

      {/* Issue Details Box if Issue */}
      {point.currentStatus === 'Issue' && (
        <div style={styles.issueBox}>
          <div style={styles.issueHeader}>
            <AlertTriangle size={14} color="#ef4444" />
            <span style={{ fontSize: '12px', fontWeight: 600, color: '#fca5a5' }}>รายละเอียดปัญหา</span>
          </div>
          {point.lastIssueTags && point.lastIssueTags.length > 0 && (
            <div style={styles.tagsContainer}>
              {point.lastIssueTags.map((tag) => (
                <span key={tag} style={styles.issueTagPill}>
                  {formatTagLabel(tag)}
                </span>
              ))}
            </div>
          )}
          {point.lastNotes && (
            <p style={styles.issueNotesText}>"{point.lastNotes}"</p>
          )}
        </div>
      )}

      {/* Footer Info */}
      <div style={styles.cardFooter}>
        <div style={styles.timeInfo}>
          <span style={styles.timeLabel}>ทำความสะอาด:</span>
          <span style={styles.timeValue}>
            {formatElapsed(point.minutesSinceLastScan, point.lastScannedAt)}
          </span>
        </div>

        {point.lastCleanerName && (
          <div style={styles.cleanerBadge}>
            <User size={12} color="#38bdf8" />
            <span>{point.lastCleanerName}</span>
          </div>
        )}
      </div>

      {/* Actions: Direct Test Link & Real Phone QR Code Modal */}
      <div style={styles.actionButtonsRow}>
        <button
          type="button"
          onClick={() => setShowQrModal(true)}
          style={styles.qrCodeBtn}
          title="สแกนด้วยกล้องมือถือจริง"
        >
          <QrCode size={14} />
          <span>QR สำหรับมือถือ</span>
        </button>

        {onOpenScanner && (
          <button
            type="button"
            onClick={() => onOpenScanner(point.qrToken)}
            style={styles.scanLinkBtn}
            title="จำลองสแกนบนเบราว์เซอร์นี้"
          >
            <span>ทดสอบสแกนบนคอม</span>
          </button>
        )}
      </div>

      {/* Real Phone QR Code Modal */}
      {showQrModal && (
        <div style={styles.modalOverlay} onClick={() => setShowQrModal(false)}>
          <div style={styles.modalContent} onClick={(e) => e.stopPropagation()}>
            <div style={styles.modalHeader}>
              <h4 style={styles.modalTitle}>สแกนบันทึกงานด้วยมือถือ</h4>
              <button onClick={() => setShowQrModal(false)} style={styles.closeBtn}>
                <X size={18} />
              </button>
            </div>

            <p style={styles.modalSubtitle}>
              ใช้แอปกล้องในมือถือ หรือ LINE สแกน QR Code นี้ (ต้องต่อ WiFi เดียวกันกับ Mac)
            </p>

            <div style={styles.qrImageWrapper}>
              <img
                src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&margin=10&data=${encodeURIComponent(scanUrl)}`}
                alt={`QR Code for ${point.name}`}
                style={styles.qrImage}
              />
            </div>

            <div style={styles.urlBox}>
              <span style={styles.urlLabel}>URL สำหรับเปิดตรง:</span>
              <a href={scanUrl} target="_blank" rel="noreferrer" style={styles.urlLink}>
                {scanUrl}
              </a>
            </div>

            <div style={styles.modalFooter}>
              <span style={{ fontSize: '12px', color: '#64748b' }}>
                🔑 บัญชีแม่บ้าน: <strong>somchai</strong> / <strong>password123</strong>
              </span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

function getBorderColor(status: PointStatus): string {
  switch (status) {
    case 'Issue':
      return 'rgba(239, 68, 68, 0.4)';
    case 'Overdue':
      return 'rgba(249, 115, 22, 0.35)';
    case 'Normal':
      return 'rgba(34, 197, 94, 0.25)';
    case 'OffHours':
      return 'rgba(100, 116, 139, 0.2)';
  }
}

function getShadow(status: PointStatus): string {
  switch (status) {
    case 'Issue':
      return '0 10px 25px -5px rgba(239, 68, 68, 0.15)';
    case 'Overdue':
      return '0 10px 25px -5px rgba(249, 115, 22, 0.12)';
    case 'Normal':
      return '0 10px 25px -5px rgba(34, 197, 94, 0.1)';
    case 'OffHours':
      return 'none';
  }
}

function formatTagLabel(tag: string): string {
  const map: Record<string, string> = {
    no_toilet_paper: 'กระดาษหมด',
    wet_floor: 'พื้นเปียก/ลื่น',
    bad_odor: 'กลิ่นเหม็น',
    trash_full: 'ถังขยะเต็ม',
    plumbing_issue: 'ท่อตัน/น้ำรั่ว',
    equipment_broken: 'อุปกรณ์ชำรุด'
  };
  return map[tag] || tag;
}

const styles: Record<string, React.CSSProperties> = {
  card: {
    background: 'rgba(30, 41, 59, 0.75)',
    border: '1px solid',
    borderRadius: '18px',
    padding: '20px 18px',
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
    transition: 'all 0.25s ease',
    backdropFilter: 'blur(12px)',
    position: 'relative'
  },
  cardHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between'
  },
  intervalTag: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
    fontSize: '11px',
    color: '#64748b'
  },
  title: {
    fontSize: '17px',
    fontWeight: 700,
    color: '#f8fafc',
    lineHeight: 1.3
  },
  locationRow: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: '13px',
    color: '#94a3b8'
  },
  issueBox: {
    background: 'rgba(239, 68, 68, 0.1)',
    border: '1px solid rgba(239, 68, 68, 0.25)',
    borderRadius: '10px',
    padding: '10px 12px',
    marginTop: '4px'
  },
  issueHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    marginBottom: '6px'
  },
  tagsContainer: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '4px',
    marginBottom: '4px'
  },
  issueTagPill: {
    fontSize: '11px',
    fontWeight: 600,
    background: 'rgba(239, 68, 68, 0.2)',
    color: '#fca5a5',
    padding: '2px 8px',
    borderRadius: '6px',
    border: '1px solid rgba(239, 68, 68, 0.3)'
  },
  issueNotesText: {
    fontSize: '12px',
    fontStyle: 'italic',
    color: '#fecaca',
    marginTop: '4px'
  },
  cardFooter: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: '12px',
    marginTop: 'auto',
    borderTop: '1px solid rgba(255, 255, 255, 0.06)'
  },
  timeInfo: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px'
  },
  timeLabel: {
    fontSize: '11px',
    color: '#64748b'
  },
  timeValue: {
    fontSize: '13px',
    fontWeight: 600,
    color: '#cbd5e1'
  },
  cleanerBadge: {
    display: 'flex',
    alignItems: 'center',
    gap: '5px',
    fontSize: '12px',
    fontWeight: 500,
    color: '#94a3b8',
    background: 'rgba(15, 23, 42, 0.6)',
    padding: '4px 8px',
    borderRadius: '8px'
  },
  actionButtonsRow: {
    display: 'flex',
    gap: '8px',
    marginTop: '6px'
  },
  qrCodeBtn: {
    flex: 1,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '6px',
    background: 'rgba(56, 189, 248, 0.12)',
    border: '1px solid rgba(56, 189, 248, 0.3)',
    borderRadius: '10px',
    padding: '9px 12px',
    color: '#38bdf8',
    fontSize: '12px',
    fontWeight: 600,
    cursor: 'pointer',
    transition: 'all 0.2s'
  },
  scanLinkBtn: {
    flex: 1,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '6px',
    background: 'rgba(255, 255, 255, 0.05)',
    border: '1px solid rgba(255, 255, 255, 0.08)',
    borderRadius: '10px',
    padding: '9px 12px',
    color: '#94a3b8',
    fontSize: '12px',
    fontWeight: 500,
    cursor: 'pointer',
    transition: 'background 0.2s'
  },
  modalOverlay: {
    position: 'fixed',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    background: 'rgba(0, 0, 0, 0.75)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1000,
    backdropFilter: 'blur(6px)',
    padding: '20px'
  },
  modalContent: {
    width: '100%',
    maxWidth: '380px',
    background: '#1e293b',
    border: '1px solid rgba(255, 255, 255, 0.15)',
    borderRadius: '20px',
    padding: '24px',
    boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.7)',
    textAlign: 'center'
  },
  modalHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '8px'
  },
  modalTitle: {
    fontSize: '17px',
    fontWeight: 700,
    color: '#f8fafc'
  },
  closeBtn: {
    background: 'transparent',
    border: 'none',
    color: '#94a3b8',
    cursor: 'pointer',
    padding: '4px'
  },
  modalSubtitle: {
    fontSize: '13px',
    color: '#94a3b8',
    lineHeight: 1.4,
    marginBottom: '16px'
  },
  qrImageWrapper: {
    padding: '12px',
    background: '#ffffff',
    borderRadius: '14px',
    display: 'inline-block',
    marginBottom: '16px',
    boxShadow: '0 10px 25px rgba(0, 0, 0, 0.3)'
  },
  qrImage: {
    width: '180px',
    height: '180px',
    display: 'block'
  },
  urlBox: {
    background: 'rgba(15, 23, 42, 0.6)',
    padding: '10px 12px',
    borderRadius: '10px',
    textAlign: 'left',
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    marginBottom: '14px'
  },
  urlLabel: {
    fontSize: '11px',
    color: '#64748b'
  },
  urlLink: {
    fontSize: '12px',
    color: '#38bdf8',
    wordBreak: 'break-all',
    textDecoration: 'none'
  },
  modalFooter: {
    paddingTop: '12px',
    borderTop: '1px solid rgba(255, 255, 255, 0.08)'
  }
};
