import { useAppSelector } from '../../store/hooks';
import { selectHubStatus } from '../../store/slices/hubSlice';
import { freshOnOpen } from '../../shared/api/baseApi';
import { MY_WORK_HUB, useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';

export function useMyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);
  const hubStatus = useAppSelector(selectHubStatus(MY_WORK_HUB));
  const fullName = useAppSelector((state) => state.auth.currentUser?.fullName);
  const own = data?.areas.find((area) => !area.isCover);
  const buildingCode = own?.points[0]?.buildingCode;
  const where = own
    ? `${own.areaCode} ${own.areaName}${buildingCode ? ` · ตึก ${buildingCode}` : ''}`
    : (fullName ?? '');

  return {
    data,
    errorMessage: error ? getErrorMessage(error) : undefined,
    isLoading,
    isFetching,
    hubStatus,
    where,
    refetch,
  };
}
