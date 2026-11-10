import projectLogo from '../assets/project-logo.png';

export function ProjectLogo() {
  return (
    <img
      src={projectLogo}
      alt="Janitorial Management Dashboard"
      width={648}
      height={216}
      className="h-auto w-54 max-w-full"
    />
  );
}
