import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/httpClient';
import { useAuth } from '../hooks/useAuth';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const session = await login({ email, password });
      const defaultDestination = session.roles.includes('Staff') || session.roles.includes('Admin') ? '/staff' : '/reservas';
      const destination = resolveReturnTo(location.search) ?? defaultDestination;
      navigate(destination, { replace: true });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos iniciar sesión.';
      setError(message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="page auth-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Ingresar a tu cuenta</h1>
      </header>

      <form className="card form-card" onSubmit={handleSubmit}>
        <label className="field">
          Email
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
        </label>

        <label className="field">
          Password
          <div className="password-input-wrap">
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
            <button
              className="password-toggle"
              type="button"
              onClick={() => setShowPassword((current) => !current)}
              aria-label={showPassword ? 'Ocultar password' : 'Mostrar password'}
              aria-pressed={showPassword}
            >
              <PasswordVisibilityIcon isVisible={showPassword} />
            </button>
          </div>
        </label>

        {error ? <p className="message error">{error}</p> : null}

        <button className="btn btn-primary" type="submit" disabled={loading}>
          {loading ? 'Ingresando...' : 'Ingresar'}
        </button>

        <p className="footnote">
          Si aun no tenés cuenta, <Link to="/register">registrate aca</Link>.
        </p>

        <p className="footnote">
          <Link to="/">Volver al inicio</Link>
        </p>
      </form>
    </main>
  );
}

type PasswordVisibilityIconProps = {
  isVisible: boolean;
};

function PasswordVisibilityIcon({ isVisible }: PasswordVisibilityIconProps) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M2 12s3.8-6 10-6 10 6 10 6-3.8 6-10 6-10-6-10-6z" />
      <circle cx="12" cy="12" r="2.8" />
      {isVisible ? <path d="M4 4l16 16" /> : null}
    </svg>
  );
}

function resolveReturnTo(search: string): string | null {
  const returnTo = new URLSearchParams(search).get('returnTo');

  if (!returnTo || !returnTo.startsWith('/') || returnTo.startsWith('//')) {
    return null;
  }

  return returnTo;
}
