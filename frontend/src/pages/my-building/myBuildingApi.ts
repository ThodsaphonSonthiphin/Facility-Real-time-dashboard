import { baseApi } from '../../shared/api/baseApi';
import { followScanHub } from '../../shared/api/scanHub';
import type { PointCard, Shift } from '../../shared/api/types';
import { getValidAccessToken } from '../../shared/auth/session';
import type { RootState } from '../../store';
import { patchPoint } from './buildingRules';

/** MyBuildingSectionDto. isCover: shown because of a Cover Assignment this shift; canInspect: may inspect there now (facility-0048). */
export interface MyBuildingSection {
  buildingId: number;
  buildingCode: string;
  buildingName: string;
  isCover: boolean;
  canInspect: boolean;
  points: PointCard[];
}

/** MyBuildingDto (facility-0057 item 1): the clock's shift; the own building first, then buildings covered in it. */
export interface MyBuilding {
  shiftDate: string;
  shift: Shift;
  buildings: MyBuildingSection[];
}

const myBuildingApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getMyBuilding: build.query<MyBuilding, void>({
      query: () => '/my-building',
      providesTags: ['MyBuilding'],
      // facility-0057 item 1 "อัปเดตเอง": pushed cards patch this entry; a reconnect asks again (#26, facility-0064)
      async onCacheEntryAdded(_arg, { cacheDataLoaded, cacheEntryRemoved, updateCachedData, dispatch, getState }) {
        await followScanHub({
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onPoint: (card) => updateCachedData((draft) => patchPoint(draft, card)),
          onMissed: () => dispatch(baseApi.util.invalidateTags(['MyBuilding'])),
        });
      },
    }),
  }),
});

export const { useGetMyBuildingQuery } = myBuildingApi;
