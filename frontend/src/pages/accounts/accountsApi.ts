import { baseApi } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';

/** UserRole as the accounts API sends it (the login answer's role is lower-case; this one is not). */
export type AccountRole = 'Cleaner' | 'Supervisor' | 'Admin';

/** AccountDto (D6). loginName is the employee ID, or an Admin's username. No phone, password or hash ever comes. */
export interface Account {
  id: number;
  role: AccountRole;
  loginName: string;
  displayName: string;
  areaId?: number;
  areaCode?: string;
  buildingId?: number;
  buildingCode?: string;
  shift?: Shift;
  isActive: boolean;
  createdAt: string;
}

/** CreateAccountRequest with only the role's own fields: the server refuses any other with 400 (plan 5 decision 9). */
export type CreateAccountRequest =
  | { role: 'Cleaner'; displayName: string; employeeId: string; phone: string; shift: Shift; areaId?: number }
  | { role: 'Supervisor'; displayName: string; employeeId: string; phone: string; buildingId: number; shift: Shift }
  | { role: 'Admin'; displayName: string; username: string; password: string };

/** facility-0020: the accounts page. Every save invalidates AdminSetup: accounts and Area slots depend on each other. */
const accountsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getAccounts: build.query<Account[], void>({
      query: () => '/admin/accounts',
      providesTags: ['AdminSetup'],
    }),
    createAccount: build.mutation<Account, CreateAccountRequest>({
      query: (body) => ({ url: '/admin/accounts', method: 'POST', body }),
      invalidatesTags: ['AdminSetup'],
    }),
  }),
});

export const { useGetAccountsQuery, useCreateAccountMutation } = accountsApi;
