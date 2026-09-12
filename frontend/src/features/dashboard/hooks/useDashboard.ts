import { useState, useEffect, useCallback, useMemo } from 'react';
import type { ServicePointStatus, PointStatus } from '../../../types';
import { getServicePointsApi } from '../../../services/api';
import { createScanHubConnection } from '../../../services/signalr';

export function useDashboard() {
  const [points, setPoints] = useState<ServicePointStatus[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedStatusFilter, setSelectedStatusFilter] = useState<PointStatus | 'All'>('All');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [lastUpdatedPointId, setLastUpdatedPointId] = useState<number | null>(null);
  const [isSignalRConnected, setIsSignalRConnected] = useState<boolean>(false);

  // Fetch initial points
  const fetchPoints = useCallback(async () => {
    try {
      setError(null);
      const data = await getServicePointsApi();
      setPoints(data);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'ไม่สามารถโหลดข้อมูลได้';
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchPoints();
  }, [fetchPoints]);

  // Setup SignalR connection
  useEffect(() => {
    const hubConnection = createScanHubConnection((updatedPoint: ServicePointStatus) => {
      setPoints((prevPoints) => {
        const index = prevPoints.findIndex((p) => p.id === updatedPoint.id);
        if (index >= 0) {
          const next = [...prevPoints];
          next[index] = updatedPoint;
          return next;
        } else {
          return [updatedPoint, ...prevPoints];
        }
      });

      // Highlight card animation
      setLastUpdatedPointId(updatedPoint.id);
      setTimeout(() => {
        setLastUpdatedPointId(null);
      }, 2000);
    });

    hubConnection
      .start()
      .then(() => {
        setIsSignalRConnected(true);
        console.log('Connected to SignalR ScanHub');
      })
      .catch((err) => {
        console.error('SignalR connection failed:', err);
        setIsSignalRConnected(false);
      });

    hubConnection.onreconnected(() => {
      setIsSignalRConnected(true);
      fetchPoints(); // Refresh snapshot on reconnect
    });

    hubConnection.onreconnecting(() => {
      setIsSignalRConnected(false);
    });

    hubConnection.onclose(() => {
      setIsSignalRConnected(false);
    });

    return () => {
      hubConnection.stop();
    };
  }, [fetchPoints]);

  // Periodic status recalculation every 30s
  useEffect(() => {
    const interval = setInterval(() => {
      fetchPoints();
    }, 30000);

    return () => clearInterval(interval);
  }, [fetchPoints]);

  // Calculate KPIs
  const kpis = useMemo(() => {
    const counts = {
      total: points.length,
      normal: 0,
      overdue: 0,
      issue: 0,
      offhours: 0
    };

    for (const p of points) {
      if (p.currentStatus === 'Normal') counts.normal++;
      else if (p.currentStatus === 'Overdue') counts.overdue++;
      else if (p.currentStatus === 'Issue') counts.issue++;
      else if (p.currentStatus === 'OffHours') counts.offhours++;
    }

    return counts;
  }, [points]);

  // Filter and sort points by priority: Issue (1) > Overdue (2) > Normal (3) > OffHours (4)
  const sortedAndFilteredPoints = useMemo(() => {
    const priorityWeight: Record<PointStatus, number> = {
      Issue: 1,
      Overdue: 2,
      Normal: 3,
      OffHours: 4
    };

    return points
      .filter((p) => {
        if (selectedStatusFilter !== 'All' && p.currentStatus !== selectedStatusFilter) {
          return false;
        }
        if (searchQuery.trim()) {
          const q = searchQuery.toLowerCase();
          return (
            p.name.toLowerCase().includes(q) ||
            p.location.toLowerCase().includes(q) ||
            (p.lastCleanerName && p.lastCleanerName.toLowerCase().includes(q))
          );
        }
        return true;
      })
      .sort((a, b) => {
        const pDiff = priorityWeight[a.currentStatus] - priorityWeight[b.currentStatus];
        if (pDiff !== 0) return pDiff;
        return a.name.localeCompare(b.name, 'th');
      });
  }, [points, selectedStatusFilter, searchQuery]);

  return {
    points: sortedAndFilteredPoints,
    rawPointsCount: points.length,
    isLoading,
    error,
    kpis,
    selectedStatusFilter,
    setSelectedStatusFilter,
    searchQuery,
    setSearchQuery,
    lastUpdatedPointId,
    isSignalRConnected,
    refresh: fetchPoints
  };
}
