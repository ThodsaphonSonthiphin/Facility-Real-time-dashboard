import projectLogo from '../assets/project-logo.png';

export function ProjectLogo({ compact = false }: { compact?: boolean }) {
  return (
    <img
      src={projectLogo}
      alt="Janitorial Management Dashboard"
      width={648}
      height={216}
      className={compact ? 'h-auto w-42 max-w-full lg:w-54' : 'h-auto w-54 max-w-full'}
    />
  );
}
