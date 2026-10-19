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

/** UpdateAccountRequest: the role and login name are fixed; the place fields replace the current ones. */
export interface UpdateAccountBody {
  displayName: string;
  areaId?: number;
  buildingId?: number;
  shift?: Shift;
}

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
    updateAccount: build.mutation<Account, { id: number; body: UpdateAccountBody }>({
      query: ({ id, body }) => ({ url: `/admin/accounts/${id}`, method: 'PUT', body }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0020: no delete. Deactivation revokes every session and frees a Cleaner's Area (plan 5 decision 3). */
    setAccountActive: build.mutation<Account, { id: number; active: boolean }>({
      query: ({ id, active }) => ({ url: `/admin/accounts/${id}/${active ? 'activate' : 'deactivate'}`, method: 'POST' }),
      invalidatesTags: ['AdminSetup'],
    }),
    /** facility-0054 rule 4. 204; every session of the person is revoked. */
    changePhone: build.mutation<void, { id: number; phone: string }>({
      query: ({ id, phone }) => ({ url: `/admin/accounts/${id}/phone`, method: 'PUT', body: { phone } }),
    }),
    /** facility-0020: an Admin's temporary password, told by voice or on paper. 204. */
    resetPassword: build.mutation<void, { id: number; password: string }>({
      query: ({ id, password }) => ({ url: `/admin/accounts/${id}/password`, method: 'PUT', body: { password } }),
    }),
  }),
});

export const {
  useGetAccountsQuery,
  useCreateAccountMutation,
  useUpdateAccountMutation,
  useSetAccountActiveMutation,
  useChangePhoneMutation,
  useResetPasswordMutation,
} = accountsApi;
