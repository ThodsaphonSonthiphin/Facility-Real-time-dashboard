import { useAppSelector } from '../../store/hooks';
import { freshOnOpen } from '../../shared/api/baseApi';
import { useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';

export function useMyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);
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
    where,
    refetch,
  };
}
