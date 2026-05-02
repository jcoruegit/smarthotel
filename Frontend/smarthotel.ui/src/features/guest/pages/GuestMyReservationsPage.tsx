import { useEffect, useState, type FormEvent } from 'react';
import { ApiError } from '../../../shared/api/httpClient';
import { useAuth } from '../../auth/hooks/useAuth';
import { listMyReservations, type GuestReservationListItem } from '../api/reservationsApi';

const reservationsPageSize = 3;

export function GuestMyReservationsPage() {
  const { session } = useAuth();

  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [reservations, setReservations] = useState<GuestReservationListItem[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session?.accessToken) {
      return;
    }

    void fetchReservationsWithFilters(fromDate, toDate);
  }, [session?.accessToken]);

  async function fetchReservationsWithFilters(nextFromDate: string, nextToDate: string) {
    if (!session?.accessToken) {
      return;
    }

    setError(null);
    if (nextFromDate && nextToDate && new Date(nextFromDate).getTime() > new Date(nextToDate).getTime()) {
      setError('El campo Desde no puede ser mayor que el campo Hasta');
      return;
    }

    setLoading(true);

    try {
      const items = await listMyReservations(session.accessToken, {
        fromDate: nextFromDate || undefined,
        toDate: nextToDate || undefined,
      });

      setReservations(items);
      setCurrentPage(1);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? normalizeFilterErrorMessage(unknownError.message) : 'No pudimos consultar tus reservas.';
      setError(message);
      setReservations([]);
      setCurrentPage(1);
    } finally {
      setLoading(false);
    }
  }

  async function handleFilterSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await fetchReservationsWithFilters(fromDate, toDate);
  }

  async function handleClearFilters() {
    setFromDate('');
    setToDate('');
    await fetchReservationsWithFilters('', '');
  }

  const totalPages = Math.max(1, Math.ceil(reservations.length / reservationsPageSize));
  const safeCurrentPage = Math.min(currentPage, totalPages);
  const pageStart = (safeCurrentPage - 1) * reservationsPageSize;
  const paginatedReservations = reservations.slice(pageStart, pageStart + reservationsPageSize);

  return (
    <main className="page guest-reservations-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Ver reservas</h1>
        <p className="subtle">
          Si no ingresas fechas se muestran todas tus reservas. Si completas fechas, se aplica el filtro.
        </p>
      </header>

      <section className="card form-card">
        <h2>Filtro por fechas</h2>
        <form className="availability-grid" onSubmit={handleFilterSubmit}>
          <label className="field">
            Desde
            <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
          </label>

          <label className="field">
            Hasta
            <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
          </label>

          <div className="home-filter-actions">
            <div className="button-row">
              <button className="btn btn-primary" type="submit" disabled={loading}>
                {loading ? 'Consultando...' : 'Aplicar filtro'}
              </button>
              <button className="btn btn-ghost" type="button" disabled={loading} onClick={handleClearFilters}>
                Limpiar filtros
              </button>
            </div>
          </div>
        </form>

        {error ? <p className="message error">{error}</p> : null}
      </section>

      <section className="room-results">
        {loading ? <p className="subtle">Cargando reservas...</p> : null}
        {!loading && !error && reservations.length === 0 ? (
          <>
            <h2>Mis reservas</h2>
            <p className="message success">No se encontraron reservas para el criterio seleccionado.</p>
          </>
        ) : null}

        {!loading && !error && reservations.length > 0 ? (
          <div className="card reservation-grid-wrap">
            <table className="reservation-grid" aria-label="Listado paginado de reservas">
              <thead>
                <tr>
                  <th>Reserva</th>
                  <th>Habitacion</th>
                  <th>Fechas</th>
                  <th>Estado</th>
                  <th>Totales</th>
                </tr>
              </thead>
              <tbody>
                {paginatedReservations.map((reservation) => (
                  <tr key={reservation.reservationId}>
                    <td>#{reservation.reservationId}</td>
                    <td>
                      Hab. {reservation.roomNumber} ({reservation.roomTypeName})
                    </td>
                    <td>
                      {reservation.checkIn} - {reservation.checkOut} ({reservation.nights} noches)
                    </td>
                    <td>{translateReservationStatus(reservation.status)}</td>
                    <td>
                      Total: {formatCurrency(reservation.totalPrice)} | Pagado: {formatCurrency(reservation.totalPaid)} | Saldo:{' '}
                      {formatCurrency(reservation.remainingBalance)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="reservation-pagination" aria-label="Paginacion de reservas">
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
    </main>
  );
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('es-AR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

function translateReservationStatus(status: string): string {
  const normalized = status.trim().toLowerCase();

  if (normalized === 'confirmed') {
    return 'Confirmada';
  }

  if (normalized === 'pending') {
    return 'Pendiente';
  }

  if (normalized === 'cancelled') {
    return 'Cancelada';
  }

  if (normalized === 'completed') {
    return 'Completada';
  }

  return status;
}

function normalizeFilterErrorMessage(message: string): string {
  if (message.includes("fromDate") && message.includes("toDate")) {
    return 'El campo Desde no puede ser mayor que el campo Hasta';
  }

  return message;
}
