import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/httpClient';
import { useAuth } from '../../auth/hooks/useAuth';
import { getAvailability, type AvailabilityResponse, type AvailableRoom } from '../api/availabilityApi';
import { getRoomImage } from '../constants/roomImages';
import type { ReservationRoomSelection } from '../types/reservationSelection';

const checkoutSelectionStorageKey = 'smarthotel.reservations.checkoutSelection';

interface GuestReservationsNavigationState {
  prefetchedAvailability?: AvailabilityResponse;
}

export function GuestReservationsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { session } = useAuth();
  const isLoggedGuest = session?.roles.includes('Guest') ?? false;

  const navigationState = (location.state as GuestReservationsNavigationState | null) ?? null;
  const prefetchedAvailability = navigationState?.prefetchedAvailability ?? null;

  const filtersFromQuery = useMemo(() => readReservationFiltersFromQuery(location.search), [location.search]);
  const defaultDates = useMemo(() => getDefaultDates(), []);
  const shouldAutoSearchFromQuery = useMemo(
    () => hasReservationFiltersInQuery(location.search) && !prefetchedAvailability,
    [location.search, prefetchedAvailability],
  );

  const [checkIn, setCheckIn] = useState(filtersFromQuery.checkIn || defaultDates.checkIn);
  const [checkOut, setCheckOut] = useState(filtersFromQuery.checkOut || defaultDates.checkOut);
  const [guests, setGuests] = useState(clampGuests(filtersFromQuery.guests ?? 2));
  const [availability, setAvailability] = useState<AvailabilityResponse | null>(prefetchedAvailability);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showLoginPopup, setShowLoginPopup] = useState(false);

  const autoSearchTriggeredRef = useRef(false);

  useEffect(() => {
    if (!shouldAutoSearchFromQuery || autoSearchTriggeredRef.current) {
      return;
    }

    autoSearchTriggeredRef.current = true;
    void searchAvailability(checkIn, checkOut, guests);
  }, [checkIn, checkOut, guests, shouldAutoSearchFromQuery]);

  async function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await searchAvailability(checkIn, checkOut, guests);
  }

  async function searchAvailability(nextCheckIn: string, nextCheckOut: string, nextGuests: number) {
    setLoading(true);
    setError(null);

    try {
      const response = await getAvailability({
        checkIn: nextCheckIn,
        checkOut: nextCheckOut,
        guests: nextGuests,
      });

      setAvailability(response);
      autoSearchTriggeredRef.current = true;

      const query = new URLSearchParams({
        checkIn: nextCheckIn,
        checkOut: nextCheckOut,
        guests: String(nextGuests),
      });

      navigate(`/reservas?${query.toString()}`, { replace: true });
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos consultar disponibilidad.';
      setAvailability(null);
      setError(message);
    } finally {
      setLoading(false);
    }
  }

  function handleReserve(room: AvailableRoom) {
    if (!session) {
      setShowLoginPopup(true);
      return;
    }

    const selection: ReservationRoomSelection = {
      checkIn,
      checkOut,
      guests,
      room,
    };

    sessionStorage.setItem(checkoutSelectionStorageKey, JSON.stringify(selection));
    navigate('/reservas/confirmar', {
      state: {
        selection,
      },
    });
  }

  return (
    <main className="page guest-reservations-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Consulta de habitaciones</h1>
        <p className="subtle">Busca disponibilidad y selecciona la habitación que quieras reservar.</p>
        <div className="button-row">
          {!isLoggedGuest ? (
            <Link className="btn btn-ghost" to="/">
              Volver al inicio
            </Link>
          ) : null}
          {session ? null : (
            <>
              <Link className="btn btn-ghost" to="/register">
                Crear cuenta
              </Link>
              <Link className="btn btn-ghost" to="/login">
                Iniciar sesión
              </Link>
            </>
          )}
        </div>
      </header>

      <section className="card form-card">
        <h2>Filtrar disponibilidad</h2>

        <form className="availability-grid" onSubmit={handleSearch}>
          <label className="field">
            Check in
            <input type="date" value={checkIn} onChange={(event) => setCheckIn(event.target.value)} required />
          </label>

          <label className="field">
            Check out
            <input type="date" value={checkOut} onChange={(event) => setCheckOut(event.target.value)} required />
          </label>

          <label className="field">
            Cantidad de huéspedes
            <input
              type="number"
              min={1}
              max={10}
              value={guests}
              onChange={(event) => setGuests(clampGuests(Number(event.target.value)))}
              required
            />
          </label>

          <div className="home-filter-actions">
            <button className="btn btn-primary" type="submit" disabled={loading}>
              {loading ? 'Consultando...' : 'Consultar disponibilidad'}
            </button>
          </div>
        </form>

        {error ? <p className="message error">{error}</p> : null}
      </section>

      {availability ? (
        <section className="room-results">
          {availability.rooms.length === 0 ? (
            <p className="message success">{availability.message || 'No hay habitaciones disponibles para ese criterio.'}</p>
          ) : (
            <>
              <h2>Habitaciones disponibles</h2>
              <div className="room-results-list">
                {availability.rooms.map((room) => (
                  <article className="room-result-card" key={room.roomId}>
                    <img className="room-result-image" src={getRoomImage(room)} alt={`Habitacion ${room.roomNumber}`} />

                    <div className="room-result-info">
                      <h3>{room.roomTypeName.toUpperCase()}</h3>
                      <p className="room-result-meta">
                        Max {room.maxCapacity} | Hab. {room.roomNumber}
                      </p>
                      <p className="room-result-features">{formatFeatures(room.features)}</p>
                    </div>

                    <aside className="room-result-actions">
                      <p className="room-price">Desde {formatCurrency(room.pricePerNight)} USD</p>
                      <button className="btn btn-ghost room-reserve-btn" type="button" onClick={() => handleReserve(room)}>
                        Reservar
                      </button>
                    </aside>
                  </article>
                ))}
              </div>
            </>
          )}
        </section>
      ) : null}

      {showLoginPopup ? (
        <div className="app-popup-backdrop" role="dialog" aria-modal="true" aria-labelledby="reserve-login-title">
          <section className="app-popup">
            <h2 id="reserve-login-title">Necesitas iniciar sesión</h2>
            <p>Para reservar una habitacion primero debes iniciar sesión.</p>
            <div className="button-row">
              <button className="btn btn-ghost" type="button" onClick={() => setShowLoginPopup(false)}>
                Cerrar
              </button>
              <Link className="btn btn-primary" to="/login" onClick={() => setShowLoginPopup(false)}>
                Ir a login
              </Link>
            </div>
          </section>
        </div>
      ) : null}
    </main>
  );
}

function getDefaultDates(): { checkIn: string; checkOut: string } {
  const checkIn = new Date();
  checkIn.setDate(checkIn.getDate() + 7);

  const checkOut = new Date(checkIn);
  checkOut.setDate(checkOut.getDate() + 1);

  return {
    checkIn: formatDateInput(checkIn),
    checkOut: formatDateInput(checkOut),
  };
}

function formatDateInput(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

function clampGuests(value: number): number {
  if (Number.isNaN(value)) {
    return 1;
  }

  return Math.max(1, Math.min(10, value));
}

function hasReservationFiltersInQuery(search: string): boolean {
  const params = new URLSearchParams(search);
  return params.has('checkIn') || params.has('checkOut') || params.has('guests');
}

function readReservationFiltersFromQuery(search: string): { checkIn: string; checkOut: string; guests?: number } {
  const params = new URLSearchParams(search);
  const guestsRaw = params.get('guests');
  const guestsParam = guestsRaw ? Number(guestsRaw) : Number.NaN;

  return {
    checkIn: params.get('checkIn') ?? '',
    checkOut: params.get('checkOut') ?? '',
    guests: Number.isNaN(guestsParam) ? undefined : clampGuests(guestsParam),
  };
}

function formatFeatures(features: string): string {
  if (!features.trim()) {
    return 'Sin caracteristicas cargadas.';
  }

  return features
    .split('|')
    .map((feature) => feature.trim().toUpperCase())
    .join(' | ');
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('es-AR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}
