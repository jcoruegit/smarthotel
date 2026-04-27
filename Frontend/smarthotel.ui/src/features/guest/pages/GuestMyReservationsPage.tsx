import { useEffect, useState, type FormEvent } from 'react';
import { ApiError } from '../../../shared/api/httpClient';
import { useAuth } from '../../auth/hooks/useAuth';
import { listMyReservations, type GuestReservationListItem } from '../api/reservationsApi';

export function GuestMyReservationsPage() {
  const { session } = useAuth();

  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [reservations, setReservations] = useState<GuestReservationListItem[]>([]);
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

    setLoading(true);
    setError(null);

    try {
      const items = await listMyReservations(session.accessToken, {
        fromDate: nextFromDate || undefined,
        toDate: nextToDate || undefined,
      });

      setReservations(items);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos consultar tus reservas.';
      setError(message);
      setReservations([]);
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
        <h2>Mis reservas</h2>
        {loading ? <p className="subtle">Cargando reservas...</p> : null}
        {!loading && reservations.length === 0 ? (
          <p className="message success">No se encontraron reservas para el criterio seleccionado.</p>
        ) : null}

        {!loading && reservations.length > 0 ? (
          <div className="room-results-list">
            {reservations.map((reservation) => (
              <article className="card reservation-list-item" key={reservation.reservationId}>
                <h3>
                  Reserva #{reservation.reservationId} - Hab. {reservation.roomNumber} ({reservation.roomTypeName})
                </h3>
                <p>
                  Check in: {reservation.checkIn} | Check out: {reservation.checkOut} | Noches: {reservation.nights}
                </p>
                <p>
                  Total: {formatCurrency(reservation.totalPrice)} | Pagado: {formatCurrency(reservation.totalPaid)} | Saldo:{' '}
                  {formatCurrency(reservation.remainingBalance)}
                </p>
                <p>Estado: {translateReservationStatus(reservation.status)}</p>
              </article>
            ))}
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
