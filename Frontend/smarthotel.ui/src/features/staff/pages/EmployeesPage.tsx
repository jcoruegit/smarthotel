import { useEffect, useState } from 'react';
import { listUsers, updateUserRoles } from '../../auth/api/authApi';
import { useAuth } from '../../auth/hooks/useAuth';
import type { AuthUserRoles, AppRole } from '../../../shared/types/auth';
import { ApiError } from '../../../shared/api/httpClient';

const roleSets: Array<{ label: string; roles: AppRole[] }> = [
  { label: 'Guest', roles: ['Guest'] },
  { label: 'Staff', roles: ['Staff'] },
  { label: 'Admin', roles: ['Admin'] },
  { label: 'Staff + Admin', roles: ['Staff', 'Admin'] },
];

export function EmployeesPage() {
  const { session } = useAuth();
  const [users, setUsers] = useState<AuthUserRoles[]>([]);
  const [loading, setLoading] = useState(true);
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    let isMounted = true;

    async function loadUsers() {
      try {
        const response = await listUsers(accessToken);
        if (isMounted) {
          setUsers(response);
        }
      } catch (unknownError) {
        if (isMounted) {
          const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos listar usuarios.';
          setError(message);
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadUsers();

    return () => {
      isMounted = false;
    };
  }, [session]);

  async function assignRoles(userId: string, roles: AppRole[]) {
    if (!session) {
      return;
    }

    const accessToken = session.accessToken;

    setUpdatingId(userId);
    setError(null);

    try {
      const updated = await updateUserRoles(userId, roles, accessToken);
      setUsers((previous) => previous.map((user) => (user.id === userId ? updated : user)));
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos actualizar roles.';
      setError(message);
    } finally {
      setUpdatingId(null);
    }
  }

  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Admin</p>
        <h1>Gestion de empleados</h1>
        <p className="subtle">Esta pantalla usa /auth/users y /auth/users/{'{id}'}/roles.</p>
      </header>

      <section className="card">
        <h2>Usuarios registrados</h2>

        {loading ? <p>Cargando usuarios...</p> : null}
        {error ? <p className="message error">{error}</p> : null}

        {!loading && users.length === 0 ? <p>No hay usuarios para mostrar.</p> : null}

        <div className="user-list">
          {users.map((user) => (
            <article key={user.id} className="user-item">
              <div>
                <p className="user-name">{user.fullName || '(Sin nombre)'}</p>
                <p className="user-email">{user.email}</p>
                <p className="user-roles">Roles: {user.roles.join(', ') || 'Sin roles'}</p>
              </div>

              <div className="button-row">
                {roleSets.map((option) => (
                  <button
                    key={`${user.id}-${option.label}`}
                    className="btn btn-ghost"
                    type="button"
                    disabled={updatingId === user.id}
                    onClick={() => {
                      void assignRoles(user.id, option.roles);
                    }}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </article>
          ))}
        </div>
      </section>

    </main>
  );
}
