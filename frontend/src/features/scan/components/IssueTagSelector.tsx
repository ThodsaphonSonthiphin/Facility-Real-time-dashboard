import React from 'react';

interface IssueTagSelectorProps {
  selectedTags: string[];
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

export const IssueTagSelector: React.FC<IssueTagSelectorProps> = ({ selectedTags, onToggleTag }) => {
  return (
    <div style={styles.container}>
      <label style={styles.label}>เลือกปัญหาที่พบ (เลือกได้มากกว่า 1 ข้อ):</label>
      <div style={styles.tagGrid}>
        {AVAILABLE_TAGS.map((tag) => {
          const isSelected = selectedTags.includes(tag.key);
          return (
            <button
              key={tag.key}
              type="button"
              onClick={() => onToggleTag(tag.key)}
              style={{
                ...styles.tagButton,
                ...(isSelected ? styles.tagButtonSelected : styles.tagButtonNormal)
              }}
            >
              <span>{tag.label}</span>
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
    gap: '10px',
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
    padding: '10px 12px',
    borderRadius: '12px',
    fontSize: '13px',
    fontWeight: 500,
    cursor: 'pointer',
    border: '1px solid transparent',
    textAlign: 'center',
    transition: 'all 0.15s ease'
  },
  tagButtonNormal: {
    background: 'rgba(30, 41, 59, 0.6)',
    color: '#94a3b8',
    borderColor: 'rgba(255, 255, 255, 0.08)'
  },
  tagButtonSelected: {
    background: 'rgba(239, 68, 68, 0.2)',
    color: '#fca5a5',
    borderColor: '#ef4444',
    boxShadow: '0 0 10px rgba(239, 68, 68, 0.2)'
  }
};
