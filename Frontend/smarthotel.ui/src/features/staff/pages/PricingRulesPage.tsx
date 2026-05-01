import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { getRoomTypes, type RoomTypeOption } from '../../guest/api/availabilityApi';
import { useAuth } from '../../auth/hooks/useAuth';
import { ApiError } from '../../../shared/api/httpClient';
import {
  createPricingRule,
  deletePricingRule,
  listPricingRules,
  updatePricingRule,
  type PricingRule,
} from '../api/pricingRulesApi';

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const rulesPageSize = 3;

export function PricingRulesPage() {
  const { session, hasRole } = useAuth();
  const canManagePricingRules = hasRole('Staff') || hasRole('Admin');

  const [roomTypes, setRoomTypes] = useState<RoomTypeOption[]>([]);
  const [rules, setRules] = useState<PricingRule[]>([]);
  const [currentPage, setCurrentPage] = useState(1);

  const [loading, setLoading] = useState(true);
  const [reloading, setReloading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [deletingRuleId, setDeletingRuleId] = useState<number | null>(null);
  const [rulePendingDelete, setRulePendingDelete] = useState<PricingRule | null>(null);

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');
  const [filterRoomTypeId, setFilterRoomTypeId] = useState('');

  const [isFormOpen, setIsFormOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [editingRuleId, setEditingRuleId] = useState<number | null>(null);
  const [roomTypeId, setRoomTypeId] = useState('');
  const [date, setDate] = useState('');
  const [price, setPrice] = useState('');
  const [reason, setReason] = useState('');

  useEffect(() => {
    const accessToken = session?.accessToken;
    if (!accessToken || !canManagePricingRules) {
      return;
    }

    let isMounted = true;
    async function loadInitialData() {
      if (!accessToken) {
        return;
      }

      setLoading(true);
      setError(null);
      setSuccess(null);

      try {
        const [roomTypeResponse, pricingRulesResponse] = await Promise.all([
          getRoomTypes(),
          listPricingRules(accessToken),
        ]);

        if (!isMounted) {
          return;
        }

        setRoomTypes(roomTypeResponse);
        setRules(pricingRulesResponse);
        setCurrentPage(1);

        if (roomTypeResponse.length > 0) {
          setRoomTypeId(String(roomTypeResponse[0].id));
        }
      } catch (unknownError) {
        if (!isMounted) {
          return;
        }

        const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos cargar las reglas de precio.';
        setError(message);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadInitialData();

    return () => {
      isMounted = false;
    };
  }, [session?.accessToken, canManagePricingRules]);

  async function reloadRules(filters?: { from?: string; to?: string; roomTypeId?: number }) {
    if (!session?.accessToken) {
      return;
    }

    setReloading(true);
    setError(null);

    try {
      const response = await listPricingRules(session.accessToken, filters);
      setRules(response);
      setCurrentPage(1);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos actualizar la lista de reglas.';
      setError(message);
    } finally {
      setReloading(false);
    }
  }

  async function handleApplyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSuccess(null);
    await reloadRules({
      from: filterFrom || undefined,
      to: filterTo || undefined,
      roomTypeId: filterRoomTypeId ? Number(filterRoomTypeId) : undefined,
    });
  }

  async function handleClearFilters() {
    setFilterFrom('');
    setFilterTo('');
    setFilterRoomTypeId('');
    setSuccess(null);
    await reloadRules();
  }

  function resetFormValues() {
    setEditingRuleId(null);
    setDate('');
    setPrice('');
    setReason('');
    setFormError(null);

    if (roomTypes.length > 0) {
      setRoomTypeId(String(roomTypes[0].id));
    } else {
      setRoomTypeId('');
    }
  }

  function handleOpenCreateForm() {
    resetFormValues();
    setIsFormOpen(true);
  }

  function handleOpenEditForm(rule: PricingRule) {
    setEditingRuleId(rule.id);
    setRoomTypeId(String(rule.roomTypeId));
    setDate(rule.date);
    setPrice(formatPriceValue(rule.price));
    setReason(rule.reason);
    setFormError(null);
    setSuccess(null);
    setIsFormOpen(true);
  }

  function handleCloseForm() {
    setIsFormOpen(false);
    setSaving(false);
    setFormError(null);
  }

  async function handleSaveRule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session?.accessToken) {
      setFormError('No hay sesion activa para guardar reglas.');
      return;
    }

    const parsedRoomTypeId = Number(roomTypeId);
    const parsedPrice = Number(price);
    const normalizedReason = reason.trim();

    if (!Number.isInteger(parsedRoomTypeId) || parsedRoomTypeId <= 0) {
      setFormError('Selecciona un tipo de habitacion valido.');
      return;
    }

    if (!DATE_PATTERN.test(date)) {
      setFormError('La fecha debe tener formato yyyy-MM-dd.');
      return;
    }

    if (!Number.isFinite(parsedPrice) || parsedPrice <= 0) {
      setFormError('El precio debe ser mayor a cero.');
      return;
    }

    if (!normalizedReason) {
      setFormError('El motivo es obligatorio.');
      return;
    }

    setSaving(true);
    setFormError(null);
    setError(null);
    setSuccess(null);

    try {
      const payload = {
        roomTypeId: parsedRoomTypeId,
        date,
        price: roundMoney(parsedPrice),
        reason: normalizedReason,
      };

      if (editingRuleId === null) {
        await createPricingRule(payload, session.accessToken);
        setSuccess('Regla creada correctamente.');
      } else {
        await updatePricingRule(editingRuleId, payload, session.accessToken);
        setSuccess('Regla actualizada correctamente.');
      }

      handleCloseForm();

      await reloadRules({
        from: filterFrom || undefined,
        to: filterTo || undefined,
        roomTypeId: filterRoomTypeId ? Number(filterRoomTypeId) : undefined,
      });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos guardar la regla.';
      setFormError(message);
    } finally {
      setSaving(false);
    }
  }

  async function handleConfirmDeleteRule() {
    if (!rulePendingDelete) {
      return;
    }

    if (!session?.accessToken) {
      return;
    }

    setDeletingRuleId(rulePendingDelete.id);
    setError(null);
    setSuccess(null);

    try {
      await deletePricingRule(rulePendingDelete.id, session.accessToken);
      setSuccess('Regla eliminada correctamente.');
      setRulePendingDelete(null);

      await reloadRules({
        from: filterFrom || undefined,
        to: filterTo || undefined,
        roomTypeId: filterRoomTypeId ? Number(filterRoomTypeId) : undefined,
      });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos eliminar la regla.';
      setError(message);
    } finally {
      setDeletingRuleId(null);
    }
  }

  const totalPages = Math.max(1, Math.ceil(rules.length / rulesPageSize));
  const safeCurrentPage = Math.min(currentPage, totalPages);
  const pageStart = (safeCurrentPage - 1) * rulesPageSize;
  const paginatedRules = rules.slice(pageStart, pageStart + rulesPageSize);

  if (!canManagePricingRules) {
    return (
      <main className="page staff-page">
        <section className="card centered-card">
          <p className="eyebrow">Acceso denegado</p>
          <h1>No tenes permisos para pricing</h1>
          <p className="subtle">Esta seccion esta habilitada solo para usuarios Staff y Admin.</p>
          <Link className="btn btn-primary" to="/staff">
            Volver al panel
          </Link>
        </section>
      </main>
    );
  }

  if (loading) {
    return (
      <main className="page staff-page">
        <section className="card centered-card">
          <p>Cargando reglas de precio...</p>
        </section>
      </main>
    );
  }

  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Staff/Admin</p>
        <h1>Pricing rules</h1>
        <p className="subtle">Gestiona reglas por tipo de habitacion y fecha. Impactan en disponibilidad y total de reservas.</p>
      </header>

      <section className="card">
        <h2>Consulta</h2>

        <form className="employee-filters" onSubmit={handleApplyFilters}>
          <label className="field">
            Desde
            <input type="date" value={filterFrom} onChange={(event) => setFilterFrom(event.target.value)} />
          </label>

          <label className="field">
            Hasta
            <input type="date" value={filterTo} onChange={(event) => setFilterTo(event.target.value)} />
          </label>

          <label className="field">
            Tipo de habitacion
            <select value={filterRoomTypeId} onChange={(event) => setFilterRoomTypeId(event.target.value)}>
              <option value="">Todos</option>
              {roomTypes.map((roomType) => (
                <option key={roomType.id} value={roomType.id}>
                  {roomType.name}
                </option>
              ))}
            </select>
          </label>

          <div className="button-row employee-filters-actions">
            <button className="btn btn-primary" type="submit" disabled={reloading}>
              {reloading ? 'Consultando...' : 'Consultar'}
            </button>
            <button className="btn btn-ghost" type="button" onClick={() => void handleClearFilters()} disabled={reloading}>
              Limpiar filtros
            </button>
            <button className="btn btn-ghost" type="button" onClick={handleOpenCreateForm}>
              Alta
            </button>
          </div>
        </form>
      </section>

      <section className="card">
        <h2>Resultados</h2>
        {error ? <p className="message error">{error}</p> : null}
        {success ? <p className="message success">{success}</p> : null}
        {rules.length === 0 ? <p className="subtle">No hay reglas para los filtros seleccionados.</p> : null}

        {rules.length > 0 ? (
          <div className="employee-table-wrap">
            <table className="employee-table" aria-label="Listado paginado de reglas de precio">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Tipo</th>
                  <th>Precio</th>
                  <th>Motivo</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {paginatedRules.map((rule) => (
                  <tr key={rule.id}>
                    <td>{rule.date}</td>
                    <td>{rule.roomTypeName}</td>
                    <td>{formatMoney(rule.price)}</td>
                    <td>{rule.reason}</td>
                    <td>
                      <div className="employee-actions">
                        <button className="btn btn-ghost icon-btn" type="button" onClick={() => handleOpenEditForm(rule)} title="Modificar">
                          <EditIcon />
                        </button>
                        <button
                          className="btn btn-ghost icon-btn"
                          type="button"
                          onClick={() => setRulePendingDelete(rule)}
                          disabled={deletingRuleId === rule.id}
                          title="Eliminar"
                        >
                          <DeleteIcon />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="reservation-pagination" aria-label="Paginacion de reglas de precio">
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

      {isFormOpen ? (
        <div className="app-popup-backdrop" role="presentation">
          <section className="app-popup" role="dialog" aria-modal="true" aria-labelledby="pricing-rule-form-title">
            <h2 id="pricing-rule-form-title">{editingRuleId === null ? 'Alta de regla' : 'Modificar regla'}</h2>

            <form className="form-card" onSubmit={handleSaveRule}>
              <label className="field">
                Tipo de habitacion
                <select value={roomTypeId} onChange={(event) => setRoomTypeId(event.target.value)} required>
                  <option value="" disabled>
                    Seleccionar...
                  </option>
                  {roomTypes.map((roomType) => (
                    <option key={roomType.id} value={roomType.id}>
                      {roomType.name}
                    </option>
                  ))}
                </select>
              </label>

              <label className="field">
                Fecha
                <input type="date" value={date} onChange={(event) => setDate(event.target.value)} required />
              </label>

              <label className="field">
                Precio
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  inputMode="decimal"
                  value={price}
                  onChange={(event) => setPrice(event.target.value)}
                  required
                />
              </label>

              <label className="field">
                Motivo
                <input value={reason} onChange={(event) => setReason(event.target.value)} maxLength={120} required />
              </label>

              {formError ? <p className="message error">{formError}</p> : null}

              <div className="button-row">
                <button className="btn btn-primary" type="submit" disabled={saving}>
                  {saving ? 'Guardando...' : editingRuleId === null ? 'Guardar alta' : 'Guardar cambios'}
                </button>
                <button className="btn btn-ghost" type="button" disabled={saving} onClick={handleCloseForm}>
                  Cancelar
                </button>
              </div>
            </form>
          </section>
        </div>
      ) : null}

      {rulePendingDelete ? (
        <div className="app-popup-backdrop" role="presentation">
          <section className="app-popup" role="dialog" aria-modal="true" aria-labelledby="delete-pricing-rule-title">
            <h2 id="delete-pricing-rule-title">Eliminar regla</h2>
            <p>
              Queres eliminar la regla del <strong>{rulePendingDelete.date}</strong> para{' '}
              <strong>{rulePendingDelete.roomTypeName}</strong>?
            </p>
            <div className="button-row">
              <button className="btn btn-primary" type="button" disabled={deletingRuleId !== null} onClick={() => void handleConfirmDeleteRule()}>
                {deletingRuleId !== null ? 'Eliminando...' : 'Aceptar'}
              </button>
              <button
                className="btn btn-ghost"
                type="button"
                disabled={deletingRuleId !== null}
                onClick={() => setRulePendingDelete(null)}
              >
                Cancelar
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </main>
  );
}

function formatMoney(value: number): string {
  return new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: 'ARS',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

function formatPriceValue(value: number): string {
  return roundMoney(value).toString();
}

function roundMoney(value: number): number {
  return Math.round(value * 100) / 100;
}

function EditIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M4 20h4l10-10-4-4L4 16v4z" />
      <path d="M13 7l4 4" />
    </svg>
  );
}

function DeleteIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path d="M3 6h18" />
      <path d="M8 6V4h8v2" />
      <path d="M6 6l1 14h10l1-14" />
      <path d="M10 10v7" />
      <path d="M14 10v7" />
    </svg>
  );
}
