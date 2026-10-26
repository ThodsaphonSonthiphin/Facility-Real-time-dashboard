import { baseApi } from './baseApi';
import type { Shift } from './types';

/**
 * The Admin's Area, point and sign data (facility-0045), read by the Areas, Area form, capture and print pages, and
 * the accounts page's pickers. Fields the server sends as null arrive missing, so they are optional here.
 */

/** facility-0040 rule 4. */
export type ShiftPattern = 'DayAndNight' | 'DayOnly';

/** facility-0045: Site = captured standing at the sign; Map = typed from a map, "ยังไม่ยืนยันหน้างาน". */
export type LocationSource = 'Site' | 'Map';

/** BuildingDto. */
export interface Building {
  id: number;
  code: string;
  name: string;
}

/** ManagedBuildingDto (facility-0090): every building, off ones too, with what is active in it. */
export interface ManagedBuilding extends Building {
  isActive: boolean;
  activeAreaCount: number;
  activeSupervisorCount: number;
}

/** CleanerOptionDto: every active Cleaner, for the Area form's pickers (D10). */
export interface CleanerOption {
  id: number;
  employeeId: string;
  displayName: string;
  shift?: Shift;
  areaId?: number;
  areaCode?: string;
}

/** AssignedCleanerDto. */
export interface AssignedCleaner {
  id: number;
  employeeId: string;
  displayName: string;
}

/** AreaSummaryDto. signsNotConfirmedOnSite counts the check-in sign and active points' signs with no site capture. */
export interface AreaSummary {
  id: number;
  code: string;
  name: string;
  buildingId: number;
  buildingCode: string;
  shiftPattern: ShiftPattern;
  isActive: boolean;
  dayCleaner?: AssignedCleaner;
  nightCleaner?: AssignedCleaner;
  activePointCount: number;
  signsNotConfirmedOnSite: number;
}

/** AdminSignDto. It carries the QR Token: only the Admin's Area and print pages get it (facility-0060). */
export interface AdminSign {
  id: number;
  code: string;
  qrToken: string;
  qrIssuedAt: string;
  latitude?: number;
  longitude?: number;
  locationAccuracyM?: number;
  locationSource?: LocationSource;
  locatedAt?: string;
  radiusM: number;
}

/** AdminRoundWindowDto. start and end are Thai wall-clock "HH:mm", as the Admin typed them (facility-0047). */
export interface AdminRoundWindow {
  id: number;
  shift: Shift;
  start: string;
  end: string;
}

/** AdminPointDto. Windows come Day first, then by time into the shift (a night 03:00 after 20:00). */
export interface AdminPoint {
  id: number;
  name: string;
  sortOrder: number;
  isActive: boolean;
  sign: AdminSign;
  roundWindows: AdminRoundWindow[];
}

/** AreaDetailDto: the Area, its one check-in sign, and its points, deactivated ones included. */
export interface AreaDetail {
  area: AreaSummary;
  checkInSign: AdminSign;
  points: AdminPoint[];
}

/** SignLocationRequest. A map pick has no accuracy. */
export interface SignLocationRequest {
  signId: number;
  latitude: number;
  longitude: number;
  accuracyM?: number;
  source: LocationSource;
}

export const adminSetupApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getAreas: build.query<AreaSummary[], void>({
      query: () => '/admin/areas',
      providesTags: ['AdminSetup'],
    }),
    getArea: build.query<AreaDetail, number>({
      query: (areaId) => `/admin/areas/${areaId}`,
      providesTags: ['AdminSetup'],
    }),
    getBuildings: build.query<Building[], void>({
      query: () => '/admin/buildings',
      providesTags: ['AdminSetup'],
    }),
    /** Every building, off ones too: the buildings page and the D7/D8 picker (facility-0090 decision 5). */
    getAllBuildings: build.query<ManagedBuilding[], void>({
      query: () => '/admin/buildings/all',
      providesTags: ['AdminSetup'],
    }),
    getCleaners: build.query<CleanerOption[], void>({
      query: () => '/admin/cleaners',
      providesTags: ['AdminSetup'],
    }),
    /** Used by the D4 sign panel (a map pick) and M17 (a site capture). */
    setSignLocation: build.mutation<AdminSign, SignLocationRequest>({
      query: ({ signId, ...body }) => ({ url: `/admin/signs/${signId}/location`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const {
  useGetAreasQuery,
  useGetAreaQuery,
  useGetBuildingsQuery,
  useGetAllBuildingsQuery,
  useGetCleanersQuery,
  useSetSignLocationMutation,
} = adminSetupApi;
