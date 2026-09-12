import React from 'react';

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
  const selectedExistingCount = existingTags.filter((t) => selectedTags.includes(t)).length;
  const newTagsCount = selectedTags.filter((t) => !existingTags.includes(t)).length;

  return (
    <div style={styles.container}>
      <div style={styles.headerRow}>
        <label style={styles.label}>
          เลือกปัญหาที่พบ (แตะเพื่อเปิด/ปิด):
        </label>
        {existingTags.length > 0 && (
          <div style={styles.legendContainer}>
            <span style={styles.legendExisting}>
              ● เดิม: {selectedExistingCount}
            </span>
            <span style={styles.legendNew}>
              ● ใหม่: {newTagsCount}
            </span>
          </div>
        )}
      </div>

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
                ...(isSelected ? styles.tagButtonSelected : styles.tagButtonNormal),
                ...(isExisting && isSelected ? styles.tagButtonExisting : {})
              }}
            >
              <div style={styles.tagInner}>
                <span style={styles.tagLabel}>{tag.label}</span>
                {isExisting && isSelected && (
                  <span style={styles.existingBadge}>⚡ เดิม</span>
                )}
                {!isExisting && isSelected && (
                  <span style={styles.newBadge}>+ ใหม่</span>
                )}
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
    gap: '10px',
    marginTop: '6px'
  },
  headerRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    flexWrap: 'wrap',
    gap: '6px'
  },
  label: {
    fontSize: '13px',
    fontWeight: 600,
    color: '#cbd5e1'
  },
  legendContainer: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    fontSize: '11px',
    fontWeight: 600
  },
  legendExisting: {
    color: '#fb923c',
    background: 'rgba(249, 115, 22, 0.15)',
    padding: '2px 6px',
    borderRadius: '4px',
    border: '1px solid rgba(249, 115, 22, 0.3)'
  },
  legendNew: {
    color: '#f87171',
    background: 'rgba(239, 68, 68, 0.15)',
    padding: '2px 6px',
    borderRadius: '4px',
    border: '1px solid rgba(239, 68, 68, 0.3)'
  },
  tagGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, 1fr)',
    gap: '8px'
  },
  tagButton: {
    padding: '11px 12px',
    borderRadius: '12px',
    fontSize: '13px',
    fontWeight: 500,
    cursor: 'pointer',
    border: '1px solid transparent',
    textAlign: 'center',
    transition: 'all 0.18s ease'
  },
  tagButtonNormal: {
    background: 'rgba(30, 41, 59, 0.6)',
    color: '#94a3b8',
    borderColor: 'rgba(255, 255, 255, 0.08)'
  },
  tagButtonSelected: {
    background: 'rgba(239, 68, 68, 0.22)',
    color: '#fecaca',
    borderColor: '#ef4444',
    boxShadow: '0 0 16px rgba(239, 68, 68, 0.4), inset 0 0 8px rgba(239, 68, 68, 0.2)',
    fontWeight: 600
  },
  tagButtonExisting: {
    background: 'rgba(249, 115, 22, 0.2)',
    color: '#fed7aa',
    borderColor: '#f97316',
    boxShadow: '0 0 16px rgba(249, 115, 22, 0.45), inset 0 0 8px rgba(249, 115, 22, 0.2)',
    fontWeight: 600
  },
  tagInner: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '4px'
  },
  tagLabel: {
    fontSize: '13px'
  },
  existingBadge: {
    fontSize: '10px',
    fontWeight: 700,
    background: 'rgba(249, 115, 22, 0.3)',
    color: '#fdba74',
    border: '1px solid rgba(249, 115, 22, 0.5)',
    padding: '1px 5px',
    borderRadius: '4px'
  },
  newBadge: {
    fontSize: '10px',
    fontWeight: 700,
    background: 'rgba(239, 68, 68, 0.3)',
    color: '#fca5a5',
    border: '1px solid rgba(239, 68, 68, 0.5)',
    padding: '1px 5px',
    borderRadius: '4px'
  }
};
