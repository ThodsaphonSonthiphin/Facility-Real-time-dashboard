import { baseApi } from '../../shared/api/baseApi';
import type { AuthResponse, LoginRequest } from '../../shared/api/types';

const authApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    login: build.mutation<AuthResponse, LoginRequest>({
      query: (body) => ({ url: '/auth/login', method: 'POST', body }),
    }),
  }),
});

export const { useLoginMutation } = authApi;
