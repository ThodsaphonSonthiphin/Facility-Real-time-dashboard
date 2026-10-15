/**
 * Shapes the server sends and accepts (facility-0022). Enums arrive as strings (JsonStringEnumConverter),
 * times as UTC ISO strings ending in "Z". Types used by one page only live in that page's folder.
 */

/** The "role" of a login or refresh answer (AuthClaims.ToClaimValue on the server). */
export type Role = 'cleaner' | 'supervisor' | 'admin';

/** AuthUserDto. username is the employee ID, or the Admin's username. */
export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: Role;
}

/** Answer of POST /api/auth/login and /api/auth/refresh. The refresh token is never here: it is an HttpOnly cookie (facility-0013). */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** facility-0054: Cleaners and Supervisors send employeeId + phone; the Admin sends username + password. */
export type LoginRequest = { employeeId: string; phone: string } | { username: string; password: string };
