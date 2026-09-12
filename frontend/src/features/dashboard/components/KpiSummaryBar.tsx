import React from 'react';
import type { PointStatus } from '../../../types';

interface KpiSummaryBarProps {
  kpis: {
    total: number;
    normal: number;
    overdue: number;
    issue: number;
    offhours: number;
  };
  activeFilter: PointStatus | 'All';
  onSelectFilter: (filter: PointStatus | 'All') => void;
}

export const KpiSummaryBar: React.FC<KpiSummaryBarProps> = ({
  kpis,
  activeFilter,
  onSelectFilter
}) => {
  const items = [
    {
      key: 'All' as const,
      label: 'ทั้งหมด',
      count: kpis.total,
      color: '#38bdf8',
      bgColor: 'rgba(56, 189, 248, 0.12)',
      borderColor: 'rgba(56, 189, 248, 0.3)'
    },
    {
      key: 'Normal' as const,
      label: 'ปกติ',
      count: kpis.normal,
      color: '#22c55e',
      bgColor: 'rgba(34, 197, 94, 0.12)',
      borderColor: 'rgba(34, 197, 94, 0.3)'
    },
    {
      key: 'Overdue' as const,
      label: 'เลยรอบกำหนด',
      count: kpis.overdue,
      color: '#f97316',
      bgColor: 'rgba(249, 115, 22, 0.12)',
      borderColor: 'rgba(249, 115, 22, 0.3)'
    },
    {
      key: 'Issue' as const,
      label: 'พบปัญหา',
      count: kpis.issue,
      color: '#ef4444',
      bgColor: 'rgba(239, 68, 68, 0.15)',
      borderColor: 'rgba(239, 68, 68, 0.35)'
    },
    {
      key: 'OffHours' as const,
      label: 'นอกเวลาทำการ',
      count: kpis.offhours,
      color: '#94a3b8',
      bgColor: 'rgba(100, 116, 139, 0.12)',
      borderColor: 'rgba(100, 116, 139, 0.25)'
    }
  ];

  return (
    <div className="kpi-bar-container">
      {items.map((item) => {
        const isActive = activeFilter === item.key;
        return (
          <button
            key={item.key}
            type="button"
            className="kpi-card"
            onClick={() => onSelectFilter(item.key)}
            style={{
              ...styles.kpiCard,
              background: isActive ? item.bgColor : 'rgba(30, 41, 59, 0.6)',
              borderColor: isActive ? item.borderColor : 'rgba(255, 255, 255, 0.08)',
              boxShadow: isActive ? `0 0 15px -3px ${item.borderColor}` : 'none'
            }}
          >
            <div style={styles.topRow}>
              <span style={styles.label}>{item.label}</span>
              <span style={{ ...styles.dot, backgroundColor: item.color }} />
            </div>
            <div className="kpi-count" style={{ ...styles.count, color: item.color }}>{item.count}</div>
          </button>
        );
      })}
    </div>
  );
};

const styles: Record<string, React.CSSProperties> = {
  container: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: '12px',
    marginBottom: '24px'
  },
  kpiCard: {
    padding: '16px 18px',
    borderRadius: '16px',
    border: '1px solid',
    cursor: 'pointer',
    textAlign: 'left',
    transition: 'all 0.2s ease',
    backdropFilter: 'blur(10px)'
  },
  topRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '8px'
  },
  label: {
    fontSize: '13px',
    fontWeight: 600,
    color: '#94a3b8'
  },
  dot: {
    width: '8px',
    height: '8px',
    borderRadius: '50%'
  },
  count: {
    fontSize: '28px',
    fontWeight: 800,
    lineHeight: 1
  }
};
