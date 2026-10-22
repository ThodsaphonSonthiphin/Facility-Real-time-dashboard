import type { Role } from '../api/types';

/** facility-0058: each role has its own first page. Only the Admin opens the Dashboard. */
export function homeOf(role: Role): string {
  switch (role) {
    case 'cleaner':
      return '/my-work';
    case 'supervisor':
      return '/my-building';
    case 'admin':
      return '/dashboard';
  }
}

export const roleLabel: Record<Role, string> = {
  cleaner: 'แม่บ้าน',
  supervisor: 'หัวหน้างาน',
  admin: 'แอดมิน',
};
