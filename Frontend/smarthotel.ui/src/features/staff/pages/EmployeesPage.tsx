import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { deleteEmployee, getDocumentTypes, listEmployees } from '../../auth/api/authApi';
import { useAuth } from '../../auth/hooks/useAuth';
import type { DocumentTypeOption, EmployeeListItem } from '../../../shared/types/auth';
import { ApiError } from '../../../shared/api/httpClient';

type EmployeeProfileFilter = '' | 'Staff' | 'Admin';
const employeesPageSize = 3;

export function EmployeesPage() {
  const navigate = useNavigate();
  const { session } = useAuth();

  const [documentTypes, setDocumentTypes] = useState<DocumentTypeOption[]>([]);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [documentTypeId, setDocumentTypeId] = useState('');
  const [documentNumber, setDocumentNumber] = useState('');
  const [profile, setProfile] = useState<EmployeeProfileFilter>('');

  const [employees, setEmployees] = useState<EmployeeListItem[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [searching, setSearching] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [employeePendingDelete, setEmployeePendingDelete] = useState<EmployeeListItem | null>(null);

  useEffect(() => {
    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    let isMounted = true;

    async function loadPage() {
      setLoading(true);
      setError(null);

      try {
        const [documentTypeResponse, employeesResponse] = await Promise.all([
          getDocumentTypes(),
          listEmployees({}, accessToken),
        ]);

        if (!isMounted) {
          return;
        }

        setDocumentTypes(documentTypeResponse);
        setEmployees(employeesResponse);
        setCurrentPage(1);
      } catch (unknownError) {
        if (!isMounted) {
          return;
        }

        const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos cargar la pantalla de empleados.';
        setError(message);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadPage();

    return () => {
      isMounted = false;
    };
  }, [session]);

  async function handleSearch(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();
    if (!session) {
      return;
    }

    setSearching(true);
    setError(null);

    try {
      const response = await listEmployees(
        {
          firstName: firstName.trim() || undefined,
          lastName: lastName.trim() || undefined,
          documentTypeId: documentTypeId ? Number(documentTypeId) : undefined,
          documentNumber: documentNumber.trim() || undefined,
          profile: profile || undefined,
        },
        session.accessToken,
      );

      setEmployees(response);
      setCurrentPage(1);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos consultar empleados.';
      setError(message);
    } finally {
      setSearching(false);
    }
  }

  async function handleClearFilters() {
    setFirstName('');
    setLastName('');
    setDocumentTypeId('');
    setDocumentNumber('');
    setProfile('');

    if (!session) {
      return;
    }

    setSearching(true);
    setError(null);

    try {
      const response = await listEmployees({}, session.accessToken);
      setEmployees(response);
      setCurrentPage(1);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos limpiar los filtros.';
      setError(message);
    } finally {
      setSearching(false);
    }
  }

  async function handleConfirmDelete() {
    if (!session || !employeePendingDelete) {
      return;
    }

    setDeleting(true);
    setError(null);

    try {
      await deleteEmployee(employeePendingDelete.employeeId, session.accessToken);
      setEmployees((previous) => previous.filter((item) => item.employeeId !== employeePendingDelete.employeeId));
      setCurrentPage(1);
      setEmployeePendingDelete(null);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos eliminar el empleado.';
      setError(message);
    } finally {
      setDeleting(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil(employees.length / employeesPageSize));
  const safeCurrentPage = Math.min(currentPage, totalPages);
  const pageStart = (safeCurrentPage - 1) * employeesPageSize;
  const paginatedEmployees = employees.slice(pageStart, pageStart + employeesPageSize);

  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Admin</p>
        <h1>Gestion de empleados</h1>
        <p className="subtle">Consulta y alta de empleados internos.</p>
      </header>

      <section className="card">
        <h2>Consulta</h2>

        <form className="employee-filters" onSubmit={handleSearch}>
          <label className="field">
            Nombre
            <input value={firstName} onChange={(event) => setFirstName(event.target.value)} />
          </label>

          <label className="field">
            Apellido
            <input value={lastName} onChange={(event) => setLastName(event.target.value)} />
          </label>

          <label className="field">
            Tipo de documento
            <select value={documentTypeId} onChange={(event) => setDocumentTypeId(event.target.value)}>
              <option value="">Todos</option>
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
              value={documentNumber}
              onChange={(event) => setDocumentNumber(event.target.value.replace(/\D/g, '').slice(0, 8))}
            />
          </label>

          <label className="field">
            Perfil
            <select value={profile} onChange={(event) => setProfile(event.target.value as EmployeeProfileFilter)}>
              <option value="">Todos</option>
              <option value="Staff">Staff</option>
              <option value="Admin">Admin</option>
            </select>
          </label>

          <div className="button-row employee-filters-actions">
            <button className="btn btn-primary" type="submit" disabled={loading || searching}>
              {searching ? 'Consultando...' : 'Consultar'}
            </button>
            <button className="btn btn-ghost" type="button" disabled={loading || searching} onClick={() => void handleClearFilters()}>
              Limpiar filtros
            </button>
            <button className="btn btn-ghost" type="button" onClick={() => navigate('/staff/empleados/alta')}>
              Alta
            </button>
          </div>
        </form>
      </section>

      <section className="card">
        <h2>Resultados</h2>
        {loading ? <p>Cargando empleados...</p> : null}
        {error ? <p className="message error">{error}</p> : null}
        {!loading && employees.length === 0 ? <p>No hay empleados para mostrar.</p> : null}

        {!loading && employees.length > 0 ? (
          <div className="employee-table-wrap">
            <table className="employee-table">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Apellido</th>
                  <th>Perfil</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {paginatedEmployees.map((employee) => (
                  <tr key={employee.employeeId}>
                    <td>{employee.firstName}</td>
                    <td>{employee.lastName}</td>
                    <td>{employee.profile}</td>
                    <td>
                      <div className="employee-actions">
                        <button
                          className="btn btn-ghost icon-btn"
                          type="button"
                          title="Modificar"
                          onClick={() => navigate(`/staff/empleados/${employee.employeeId}/modificar`)}
                        >
                          <PencilIcon />
                        </button>
                        {session?.userId !== employee.userId ? (
                          <button
                            className="btn btn-ghost icon-btn"
                            type="button"
                            title="Eliminar"
                            onClick={() => setEmployeePendingDelete(employee)}
                          >
                            <TrashIcon />
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="reservation-pagination" aria-label="Paginacion de empleados">
              <button
                className="btn btn-ghost"
                type="button"
                disabled={safeCurrentPage === 1}
                onClick={() => setCurrentPage((previousPage) => Math.max(previousPage - 1, 1))}
              >
                Anterior
              </button>

              {Array.from({ length: totalPages }, (_, index) => index + 1).map((pageNumber) => (
                <button
                  key={pageNumber}
                  className={`btn ${pageNumber === safeCurrentPage ? 'btn-primary' : 'btn-ghost'}`}
                  type="button"
                  onClick={() => setCurrentPage(pageNumber)}
                >
                  {pageNumber}
                </button>
              ))}

              <button
                className="btn btn-ghost"
                type="button"
                disabled={safeCurrentPage === totalPages}
                onClick={() => setCurrentPage((previousPage) => Math.min(previousPage + 1, totalPages))}
              >
                Siguiente
              </button>
            </div>
          </div>
        ) : null}
      </section>

      {employeePendingDelete ? (
        <div className="app-popup-backdrop" role="presentation">
          <section className="app-popup" role="dialog" aria-modal="true" aria-labelledby="delete-employee-title">
            <h2 id="delete-employee-title">Eliminar empleado</h2>
            <p>
              ¿Querés eliminar al empleado <strong>{employeePendingDelete.firstName} {employeePendingDelete.lastName}</strong>?
            </p>
            <div className="button-row">
              <button className="btn btn-primary" type="button" disabled={deleting} onClick={() => void handleConfirmDelete()}>
                {deleting ? 'Eliminando...' : 'Aceptar'}
              </button>
              <button className="btn btn-ghost" type="button" disabled={deleting} onClick={() => setEmployeePendingDelete(null)}>
                Cancelar
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </main>
  );
}

function PencilIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M3 17.2V21h3.8L18.2 9.6l-3.8-3.8L3 17.2z" />
      <path d="M14.4 5.8l3.8 3.8" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M4 7h16" />
      <path d="M9 7V4h6v3" />
      <path d="M19 7l-1 13H6L5 7" />
      <path d="M10 11v6M14 11v6" />
    </svg>
  );
}
