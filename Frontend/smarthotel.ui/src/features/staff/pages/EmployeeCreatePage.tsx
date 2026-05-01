import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { changePassword, createEmployee, getDocumentTypes, getEmployeeById, updateEmployee } from '../../auth/api/authApi';
import { useAuth } from '../../auth/hooks/useAuth';
import { ApiError } from '../../../shared/api/httpClient';
import type { CreateEmployeeResponse, DocumentTypeOption, EmployeeListItem } from '../../../shared/types/auth';

type EmployeeProfile = 'Staff' | 'Admin';

export function EmployeeCreatePage() {
  const { session } = useAuth();
  const navigate = useNavigate();
  const params = useParams<{ employeeId?: string }>();
  const employeeId = params.employeeId ? Number(params.employeeId) : null;
  const isEditMode = Number.isFinite(employeeId) && employeeId !== null;

  const [documentTypes, setDocumentTypes] = useState<DocumentTypeOption[]>([]);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [documentTypeId, setDocumentTypeId] = useState('');
  const [documentNumber, setDocumentNumber] = useState('');
  const [birthDate, setBirthDate] = useState('1990-01-01');
  const [profile, setProfile] = useState<EmployeeProfile>('Staff');
  const [email, setEmail] = useState<string | null>(null);
  const [editingUserId, setEditingUserId] = useState<string | null>(null);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  const [createdEmployee, setCreatedEmployee] = useState<CreateEmployeeResponse | null>(null);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const title = useMemo(() => (isEditMode ? 'Modificacion de empleado' : 'Alta de empleado'), [isEditMode]);
  const isSelfEdit = isEditMode && session?.userId === editingUserId;

  useEffect(() => {
    if (!session) {
      return;
    }
    const accessToken = session.accessToken;

    let isMounted = true;
    async function loadData() {
      setLoading(true);
      setError(null);

      try {
        const documentTypeResponse = await getDocumentTypes();
        if (!isMounted) {
          return;
        }

        setDocumentTypes(documentTypeResponse);

        if (isEditMode && employeeId !== null) {
          const employee = await getEmployeeById(employeeId, accessToken);
          if (!isMounted) {
            return;
          }

          hydrateFormFromEmployee(employee);
          return;
        }

        if (documentTypeResponse.length > 0) {
          setDocumentTypeId(String(documentTypeResponse[0].id));
        }
      } catch (unknownError) {
        if (!isMounted) {
          return;
        }

        const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos cargar los datos del formulario.';
        setError(message);
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
  }, [session, employeeId, isEditMode]);

  async function handleSaveEmployee(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!session || !documentTypeId) {
      return;
    }

    setSaving(true);
    setError(null);
    setCreatedEmployee(null);

    try {
      if (isEditMode && employeeId !== null) {
        await updateEmployee(
          employeeId,
          {
            firstName: firstName.trim(),
            lastName: lastName.trim(),
            documentTypeId: Number(documentTypeId),
            documentNumber: documentNumber.trim(),
            birthDate,
            profile,
          },
          session.accessToken,
        );

        navigate('/staff/empleados', { replace: true });
        return;
      }

      const response = await createEmployee(
        {
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          documentTypeId: Number(documentTypeId),
          documentNumber: documentNumber.trim(),
          birthDate,
          profile,
        },
        session.accessToken,
      );

      setCreatedEmployee(response);
      setEmail(response.email);
      setEditingUserId(response.userId);
      setFirstName('');
      setLastName('');
      setDocumentNumber('');
      setProfile('Staff');
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos guardar el empleado.';
      setError(message);
    } finally {
      setSaving(false);
    }
  }

  function hydrateFormFromEmployee(employee: EmployeeListItem) {
    setFirstName(employee.firstName);
    setLastName(employee.lastName);
    setDocumentTypeId(String(employee.documentTypeId));
    setDocumentNumber(employee.documentNumber);
    setBirthDate(employee.birthDate);
    setProfile(employee.profile);
    setEmail(employee.email);
    setEditingUserId(employee.userId);
  }

  async function handleSavePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session?.accessToken) {
      return;
    }

    if (!currentPassword.trim() || !newPassword.trim()) {
      setPasswordError('Completa la clave actual y la nueva clave.');
      setPasswordSuccess(null);
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError('La confirmacion de clave no coincide.');
      setPasswordSuccess(null);
      return;
    }

    setSavingPassword(true);
    setPasswordError(null);
    setPasswordSuccess(null);

    try {
      await changePassword(
        {
          currentPassword,
          newPassword,
        },
        session.accessToken,
      );

      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setPasswordSuccess('Clave actualizada correctamente.');
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos actualizar la clave.';
      setPasswordError(message);
      setPasswordSuccess(null);
    } finally {
      setSavingPassword(false);
    }
  }

  if (loading) {
    return (
      <main className="page staff-page">
        <section className="card centered-card">
          <p>Cargando formulario...</p>
        </section>
      </main>
    );
  }

  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Admin</p>
        <h1>{title}</h1>
        <p className="subtle">
          {isEditMode
            ? 'Edita la informacion del empleado.'
            : 'Se genera email automatico y password temporal al guardar.'}
        </p>
      </header>

      <section className="card">
        <form className="form-card" onSubmit={handleSaveEmployee}>
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
            <select value={documentTypeId} onChange={(event) => setDocumentTypeId(event.target.value)} required>
              {documentTypes.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            Numero de documento
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
            Perfil
            <select value={profile} onChange={(event) => setProfile(event.target.value as EmployeeProfile)} required>
              <option value="Staff">Staff</option>
              <option value="Admin">Admin</option>
            </select>
          </label>

          {email ? (
            <p className="footnote">
              Email actual: <strong>{email}</strong>
            </p>
          ) : null}

          {error ? <p className="message error">{error}</p> : null}

          <div className="button-row">
            <button className="btn btn-primary" type="submit" disabled={saving}>
              {saving ? 'Guardando...' : isEditMode ? 'Guardar cambios' : 'Guardar empleado'}
            </button>
            <Link className="btn btn-ghost" to="/staff/empleados">
              Volver a consulta
            </Link>
          </div>
        </form>
      </section>

      {isSelfEdit ? (
        <section className="card">
          <form className="form-card" onSubmit={handleSavePassword}>
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
                <input
                  type={showNewPassword ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  required
                />
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
                  aria-label={showConfirmPassword ? 'Ocultar confirmacion de clave' : 'Mostrar confirmacion de clave'}
                  aria-pressed={showConfirmPassword}
                >
                  <PasswordVisibilityIcon isVisible={showConfirmPassword} />
                </button>
              </div>
            </label>

            {passwordError ? <p className="message error">{passwordError}</p> : null}
            {passwordSuccess ? <p className="message success">{passwordSuccess}</p> : null}

            <button className="btn btn-primary" type="submit" disabled={savingPassword}>
              {savingPassword ? 'Actualizando...' : 'Guardar clave'}
            </button>
          </form>
        </section>
      ) : null}

      {!isEditMode && createdEmployee ? (
        <section className="card">
          <h2>Empleado creado</h2>
          <p className="subtle">Comparte estos datos con el empleado para su primer ingreso.</p>
          <div className="employee-credentials">
            <p>
              <strong>Nombre:</strong> {createdEmployee.fullName}
            </p>
            <p>
              <strong>Email:</strong> {createdEmployee.email}
            </p>
            <p>
              <strong>Perfil:</strong> {createdEmployee.profile}
            </p>
            <p>
              <strong>Password temporal:</strong> {createdEmployee.temporaryPassword}
            </p>
          </div>
        </section>
      ) : null}
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
