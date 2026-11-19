import { CircleAlert } from 'lucide-react';
import { Link } from 'react-router';
import { buttonClass, statusScreenClass } from './styles';

interface StatusScreenProps {
  title: string;
  detail?: string;
  linkTo?: string;
  linkText?: string;
}

/** The full-page message of "not found", "no permission" and similar dead ends. */
export function StatusScreen({ title, detail, linkTo, linkText }: StatusScreenProps) {
  return (
    <main className={statusScreenClass.main}>
      <div className={statusScreenClass.card}>
        <CircleAlert aria-hidden="true" className="size-8 self-center text-text-2" />
        <h1 className={statusScreenClass.title}>{title}</h1>
        {detail && <p className="text-text-2">{detail}</p>}
        {linkTo && linkText && (
          <Link className={buttonClass('primary', 'main')} data-tap="" to={linkTo}>
            {linkText}
          </Link>
        )}
      </div>
    </main>
  );
}
