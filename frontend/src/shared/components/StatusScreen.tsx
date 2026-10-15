import { Link } from 'react-router';
import './shared.css';

interface StatusScreenProps {
  title: string;
  detail?: string;
  linkTo?: string;
  linkText?: string;
}

/** The full-page message of "not found", "no permission" and similar dead ends. */
export function StatusScreen({ title, detail, linkTo, linkText }: StatusScreenProps) {
  return (
    <main className="app-status">
      <h1 className="app-status__title">{title}</h1>
      {detail && <p className="app-status__detail">{detail}</p>}
      {linkTo && linkText && (
        <Link className="app-button app-button--primary" to={linkTo}>
          {linkText}
        </Link>
      )}
    </main>
  );
}
