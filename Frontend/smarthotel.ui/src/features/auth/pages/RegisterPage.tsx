import { useEffect, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/httpClient';
import type { DocumentTypeOption } from '../../../shared/types/auth';
import { getDocumentTypes } from '../api/authApi';
import { useAuth } from '../hooks/useAuth';

export function RegisterPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { register, login } = useAuth();
  const adultBirthDateLimit = getAdultBirthDateLimit();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [documentTypeId, setDocumentTypeId] = useState('');
  const [documentNumber, setDocumentNumber] = useState('');
  const [birthDate, setBirthDate] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [documentTypes, setDocumentTypes] = useState<DocumentTypeOption[]>([]);
  const [loadingDocumentTypes, setLoadingDocumentTypes] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function handleDocumentNumberChange(value: string) {
    const digitsOnly = value.replace(/\D/g, '').slice(0, 8);
    setDocumentNumber(digitsOnly);
  }

  useEffect(() => {
    let isMounted = true;

    async function loadDocumentTypes() {
      try {
        const response = await getDocumentTypes();
        if (!isMounted) {
          return;
        }

        setDocumentTypes(response);
        if (response.length > 0) {
          setDocumentTypeId(String(response[0].id));
        }
      } catch {
        if (isMounted) {
          setError('No pudimos cargar los tipos de documento.');
        }
      } finally {
        if (isMounted) {
          setLoadingDocumentTypes(false);
        }
      }
    }

    void loadDocumentTypes();

    return () => {
      isMounted = false;
    };
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLoading(true);
    setError(null);

    if (password !== confirmPassword) {
      setLoading(false);
      setError('Las passwords no coinciden.');
      return;
    }

    const normalizedFirstName = firstName.trim();
    const normalizedLastName = lastName.trim();
    const normalizedDocumentNumber = documentNumber.trim();
    const normalizedEmail = email.trim();
    const parsedDocumentTypeId = Number(documentTypeId);

    if (!normalizedFirstName) {
      setLoading(false);
      setError('El nombre es obligatorio.');
      return;
    }

    if (!normalizedLastName) {
      setLoading(false);
      setError('El apellido es obligatorio.');
      return;
    }

    if (!documentTypeId || Number.isNaN(parsedDocumentTypeId) || parsedDocumentTypeId <= 0) {
      setLoading(false);
      setError('Debes seleccionar un tipo de documento.');
      return;
    }

    if (!normalizedDocumentNumber) {
      setLoading(false);
      setError('El número de documento es obligatorio.');
      return;
    }

    if (!/^\d{7,8}$/.test(normalizedDocumentNumber)) {
      setLoading(false);
      setError('El número de documento debe tener al menos 7 digitos y como maximo 8, usando solo numeros.');
      return;
    }

    if (!birthDate) {
      setLoading(false);
      setError('La fecha de nacimiento es obligatoria.');
      return;
    }

    if (!isAtLeastAge(birthDate, 18)) {
      setLoading(false);
      setError('Solo pueden crear cuenta personas de 18 anos o mas.');
      return;
    }

    try {
      await register({
        firstName: normalizedFirstName,
        lastName: normalizedLastName,
        documentTypeId: parsedDocumentTypeId,
        documentNumber: normalizedDocumentNumber,
        birthDate,
        email: normalizedEmail,
        password,
      });

      const session = await login({ email: normalizedEmail, password });
      const defaultDestination = session.roles.includes('Staff') || session.roles.includes('Admin') ? '/staff' : '/reservas';
      const destination = resolveReturnTo(location.search) ?? defaultDestination;
      navigate(destination, { replace: true });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos registrar la cuenta.';
      setError(message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="page auth-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Crear cuenta</h1>
      </header>

      <form className="card form-card" onSubmit={handleSubmit}>
        <label className="field">
          Nombre
          <input type="text" value={firstName} onChange={(event) => setFirstName(event.target.value)} required />
        </label>

        <label className="field">
          Apellido
          <input type="text" value={lastName} onChange={(event) => setLastName(event.target.value)} required />
        </label>

        <label className="field">
          Tipo de documento
          <select
            value={documentTypeId}
            onChange={(event) => setDocumentTypeId(event.target.value)}
            disabled={loadingDocumentTypes || documentTypes.length === 0}
            required
          >
            {documentTypes.length === 0 ? <option value="">Sin opciones</option> : null}
            {documentTypes.map((documentType) => (
              <option key={documentType.id} value={documentType.id}>
                {documentType.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          Número de documento
          <input
            type="text"
            inputMode="numeric"
            minLength={7}
            maxLength={8}
            value={documentNumber}
            onChange={(event) => handleDocumentNumberChange(event.target.value)}
            required
          />
        </label>

        <label className="field">
          Fecha de nacimiento
          <input
            type="date"
            value={birthDate}
            onChange={(event) => setBirthDate(event.target.value)}
            max={adultBirthDateLimit}
            required
          />
        </label>

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

        <label className="field">
          Repetir password
          <div className="password-input-wrap">
            <input
              type={showConfirmPassword ? 'text' : 'password'}
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              required
            />
            <button
              className="password-toggle"
              type="button"
              onClick={() => setShowConfirmPassword((current) => !current)}
              aria-label={showConfirmPassword ? 'Ocultar password' : 'Mostrar password'}
              aria-pressed={showConfirmPassword}
            >
              <PasswordVisibilityIcon isVisible={showConfirmPassword} />
            </button>
          </div>
        </label>

        {error ? <p className="message error">{error}</p> : null}
        {loadingDocumentTypes ? <p className="message success">Cargando tipos de documento...</p> : null}

        <button className="btn btn-primary" type="submit" disabled={loading || loadingDocumentTypes || documentTypes.length === 0}>
          {loading ? 'Creando cuenta...' : 'Crear cuenta'}
        </button>

        <p className="footnote">
          Si ya tenés cuenta, <Link to="/login">inicia sesión</Link>.
        </p>

        <p className="footnote">
          <Link to="/">Volver al inicio</Link>
        </p>
      </form>
    </main>
  );
}

function resolveReturnTo(search: string): string | null {
  const returnTo = new URLSearchParams(search).get('returnTo');

  if (!returnTo || !returnTo.startsWith('/') || returnTo.startsWith('//')) {
    return null;
  }

  return returnTo;
}

function isAtLeastAge(birthDateIso: string, minimumAge: number): boolean {
  const birthDate = new Date(`${birthDateIso}T00:00:00`);
  if (Number.isNaN(birthDate.getTime())) {
    return false;
  }

  const today = new Date();
  let age = today.getFullYear() - birthDate.getFullYear();
  const monthDiff = today.getMonth() - birthDate.getMonth();

  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
    age -= 1;
  }

  return age >= minimumAge;
}

function getAdultBirthDateLimit(): string {
  const date = new Date();
  date.setFullYear(date.getFullYear() - 18);

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
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
