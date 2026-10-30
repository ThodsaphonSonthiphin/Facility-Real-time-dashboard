import { useId, useState, type FormEvent } from 'react';
import { useLocation } from 'react-router';
import { useLogin } from './useLogin';

const wrongSecretText = 'ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง';

/** D0 presentation state; authentication, throttling and redirects stay in the shared login flow. */
export function useAdminLoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [passwordVisible, setPasswordVisible] = useState(false);
  const passwordId = useId();
  const errorId = useId();
  const location = useLocation();
  const { submit, ...login } = useLogin(wrongSecretText);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!login.isLoading) {
      void submit({ username: username.trim(), password });
    }
  };

  return {
    ...login,
    username, setUsername, password, setPassword, passwordVisible, passwordId, errorId,
    credentialInvalid: login.errorText === wrongSecretText,
    linkState: location.state,
    togglePassword: () => setPasswordVisible((visible) => !visible),
    onSubmit,
  };
}
