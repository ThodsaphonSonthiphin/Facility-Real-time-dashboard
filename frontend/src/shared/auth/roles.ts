import type { Role } from '../api/types';

/** facility-0058, facility-0224: each role has its own first page. Admin lands on Overview. */
export function homeOf(role: Role): string {
  switch (role) {
    case 'cleaner':
      return '/my-work';
    case 'supervisor':
      return '/my-building';
    case 'admin':
      return '/admin/overview';
  }
}

export const roleLabel: Record<Role, string> = {
  cleaner: 'แม่บ้าน',
  supervisor: 'หัวหน้างาน',
  admin: 'แอดมิน',
};
