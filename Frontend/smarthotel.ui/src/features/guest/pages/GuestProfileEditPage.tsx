import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/httpClient';
import { changePassword, getCurrentGuestProfile, getDocumentTypes, updateCurrentGuestProfile } from '../../auth/api/authApi';
import { useAuth } from '../../auth/hooks/useAuth';
import type { DocumentTypeOption } from '../../../shared/types/auth';

export function GuestProfileEditPage() {
  const navigate = useNavigate();
  const { session, updateSessionEmail } = useAuth();

  const [documentTypes, setDocumentTypes] = useState<DocumentTypeOption[]>([]);
  const [documentTypeId, setDocumentTypeId] = useState(1);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [documentNumber, setDocumentNumber] = useState('');
  const [birthDate, setBirthDate] = useState('1990-01-01');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [loading, setLoading] = useState(true);
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  useEffect(() => {
    const accessToken = session?.accessToken;
    const sessionEmail = session?.email;

    if (typeof accessToken !== 'string' || accessToken.length === 0) {
      return;
    }
    const accessTokenValue: string = accessToken;

    let isMounted = true;

    async function loadData() {
      setLoading(true);
      setProfileError(null);

      try {
        const [options, profile] = await Promise.all([
          getDocumentTypes(),
          getCurrentGuestProfile(accessTokenValue),
        ]);

        if (!isMounted) {
          return;
        }

        setDocumentTypes(options);

        if (profile) {
          setDocumentTypeId(profile.documentTypeId);
          setFirstName(profile.firstName);
          setLastName(profile.lastName);
          setDocumentNumber(profile.documentNumber);
          setBirthDate(profile.birthDate);
          setEmail(profile.email ?? sessionEmail ?? '');
          setPhone(profile.phone ?? '');
          return;
        }

        const fallbackDocumentTypeId = options[0]?.id ?? 1;
        setDocumentTypeId(fallbackDocumentTypeId);
        setEmail(sessionEmail ?? '');
      } catch (unknownError) {
        if (!isMounted) {
          return;
        }

        const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos cargar tus datos.';
        setProfileError(message);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadData();

    return () => {
      isMounted = false;
    };
  }, [session?.accessToken, session?.email]);

  async function handleSaveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session?.accessToken) {
      return;
    }

    setSavingProfile(true);
    setProfileError(null);

    try {
      const response = await updateCurrentGuestProfile(
        {
          documentTypeId,
          firstName,
          lastName,
          documentNumber,
          birthDate,
          email,
          phone: phone.trim() || null,
        },
        session.accessToken,
      );

      updateSessionEmail(response.email ?? email);
      navigate('/guest/panel', { replace: true });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos actualizar tus datos.';
      setProfileError(message);
    } finally {
      setSavingProfile(false);
    }
  }

  async function handleSavePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session?.accessToken) {
      return;
    }

    if (!currentPassword.trim() || !newPassword.trim()) {
      setPasswordError('Completa la clave actual y la nueva clave.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError('La confirmación de clave no coincide.');
      return;
    }

    setSavingPassword(true);
    setPasswordError(null);

    try {
      await changePassword(
        {
          currentPassword,
          newPassword,
        },
        session.accessToken,
      );

      navigate('/guest/panel', { replace: true });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos actualizar tu clave.';
      setPasswordError(message);
    } finally {
      setSavingPassword(false);
    }
  }

  if (loading) {
    return (
      <main className="page guest-reservations-page">
        <section className="card centered-card">
          <p>Cargando datos...</p>
        </section>
      </main>
    );
  }

  return (
    <main className="page guest-reservations-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Modificar datos</h1>
        <p className="subtle">Actualiza tu información personal y la clave de acceso.</p>
      </header>

      <section className="grid-cards">
        <form className="card form-card" onSubmit={handleSaveProfile}>
          <h2>Datos personales</h2>

          <label className="field">
            Nombre
            <input value={firstName} onChange={(event) => setFirstName(event.target.value)} required />
          </label>

          <label className="field">
            Apellido
            <input value={lastName} onChange={(event) => setLastName(event.target.value)} required />
          </label>

          <label className="field">
            Tipo de documento
            <select value={documentTypeId} onChange={(event) => setDocumentTypeId(Number(event.target.value))} required>
              {documentTypes.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            Número de documento
            <input
              inputMode="numeric"
              minLength={7}
              maxLength={8}
              value={documentNumber}
              onChange={(event) => setDocumentNumber(event.target.value.replace(/\D/g, '').slice(0, 8))}
              required
            />
          </label>

          <label className="field">
            Fecha de nacimiento
            <input type="date" value={birthDate} onChange={(event) => setBirthDate(event.target.value)} required />
          </label>

          <label className="field">
            Email
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
          </label>

          <label className="field">
            Telefono
            <input value={phone} onChange={(event) => setPhone(event.target.value)} />
          </label>

          {profileError ? <p className="message error">{profileError}</p> : null}

          <button className="btn btn-primary" type="submit" disabled={savingProfile}>
            {savingProfile ? 'Guardando...' : 'Guardar datos'}
          </button>
        </form>

        <form className="card form-card" onSubmit={handleSavePassword}>
          <h2>Cambiar clave</h2>

          <label className="field">
            Clave actual
            <div className="password-input-wrap">
              <input
                type={showCurrentPassword ? 'text' : 'password'}
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                required
              />
              <button
                className="password-toggle"
                type="button"
                onClick={() => setShowCurrentPassword((current) => !current)}
                aria-label={showCurrentPassword ? 'Ocultar clave actual' : 'Mostrar clave actual'}
                aria-pressed={showCurrentPassword}
              >
                <PasswordVisibilityIcon isVisible={showCurrentPassword} />
              </button>
            </div>
          </label>

          <label className="field">
            Nueva clave
            <div className="password-input-wrap">
              <input type={showNewPassword ? 'text' : 'password'} value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required />
              <button
                className="password-toggle"
                type="button"
                onClick={() => setShowNewPassword((current) => !current)}
                aria-label={showNewPassword ? 'Ocultar nueva clave' : 'Mostrar nueva clave'}
                aria-pressed={showNewPassword}
              >
                <PasswordVisibilityIcon isVisible={showNewPassword} />
              </button>
            </div>
          </label>

          <label className="field">
            Confirmar nueva clave
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
                aria-label={showConfirmPassword ? 'Ocultar confirmación de clave' : 'Mostrar confirmación de clave'}
                aria-pressed={showConfirmPassword}
              >
                <PasswordVisibilityIcon isVisible={showConfirmPassword} />
              </button>
            </div>
          </label>

          {passwordError ? <p className="message error">{passwordError}</p> : null}

          <button className="btn btn-primary" type="submit" disabled={savingPassword}>
            {savingPassword ? 'Actualizando...' : 'Guardar clave'}
          </button>
        </form>
      </section>
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
