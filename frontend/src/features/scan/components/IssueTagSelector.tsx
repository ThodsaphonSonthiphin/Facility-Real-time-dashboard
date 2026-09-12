import React from 'react';
import { Check } from 'lucide-react';

interface IssueTagSelectorProps {
  selectedTags: string[];
  existingTags?: string[];
  onToggleTag: (tagKey: string) => void;
}

const AVAILABLE_TAGS = [
  { key: 'no_toilet_paper', label: '🧻 กระดาษหมด' },
  { key: 'wet_floor', label: '💧 พื้นเปียก/ลื่น' },
  { key: 'bad_odor', label: '👃 กลิ่นเหม็น' },
  { key: 'trash_full', label: '🗑️ ถังขยะเต็ม' },
  { key: 'plumbing_issue', label: '🔧 ท่อตัน/น้ำรั่ว' },
  { key: 'equipment_broken', label: '⚠️ อุปกรณ์ชำรุด' }
];

export const IssueTagSelector: React.FC<IssueTagSelectorProps> = ({
  selectedTags,
  existingTags = [],
  onToggleTag
}) => {
  return (
    <div style={styles.container}>
      <label style={styles.label}>
        เลือกปัญหาที่พบ (แตะเพื่อเลือก / ยกเลิก):
      </label>

      <div style={styles.tagGrid}>
        {AVAILABLE_TAGS.map((tag) => {
          const isSelected = selectedTags.includes(tag.key);
          const isExisting = existingTags.includes(tag.key);

          return (
            <button
              key={tag.key}
              type="button"
              onClick={() => onToggleTag(tag.key)}
              style={{
                ...styles.tagButton,
                ...(isSelected
                  ? isExisting
                    ? styles.tagSelectedExisting
                    : styles.tagSelectedNew
                  : styles.tagNormal)
              }}
            >
              <div style={styles.tagContent}>
                <span style={styles.tagLabel}>{tag.label}</span>
                {isExisting && isSelected && (
                  <span style={styles.existingLabel}>ปัญหาเดิม</span>
                )}
                {!isExisting && isSelected && (
                  <span style={styles.newLabel}>เพิ่มใหม่</span>
                )}
              </div>

              <div
                style={{
                  ...styles.checkboxCircle,
                  ...(isSelected
                    ? isExisting
                      ? styles.checkboxCircleExisting
                      : styles.checkboxCircleNew
                    : styles.checkboxCircleUnchecked)
                }}
              >
                {isSelected && <Check size={12} color="#ffffff" strokeWidth={3} />}
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
};

const styles: Record<string, React.CSSProperties> = {
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
    marginTop: '6px'
  },
  label: {
    fontSize: '13px',
    fontWeight: 600,
    color: '#cbd5e1'
  },
  tagGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, 1fr)',
    gap: '8px'
  },
  tagButton: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '12px 14px',
    borderRadius: '12px',
    cursor: 'pointer',
    textAlign: 'left',
    transition: 'background 0.15s, border-color 0.15s'
  },
  tagNormal: {
    background: 'rgba(30, 41, 59, 0.6)',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    color: '#94a3b8'
  },
  tagSelectedExisting: {
    background: 'rgba(249, 115, 22, 0.15)',
    border: '1.5px solid #f97316',
    color: '#f8fafc'
  },
  tagSelectedNew: {
    background: 'rgba(239, 68, 68, 0.15)',
    border: '1.5px solid #ef4444',
    color: '#f8fafc'
  },
  tagContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: '3px'
  },
  tagLabel: {
    fontSize: '13px',
    fontWeight: 600
  },
  existingLabel: {
    fontSize: '10px',
    fontWeight: 600,
    color: '#fb923c'
  },
  newLabel: {
    fontSize: '10px',
    fontWeight: 600,
    color: '#f87171'
  },
  checkboxCircle: {
    width: '20px',
    height: '20px',
    borderRadius: '50%',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0
  },
  checkboxCircleUnchecked: {
    border: '1.5px solid #64748b',
    background: 'transparent'
  },
  checkboxCircleExisting: {
    background: '#ea580c',
    border: '1.5px solid #ea580c'
  },
  checkboxCircleNew: {
    background: '#dc2626',
    border: '1.5px solid #dc2626'
  }
};
