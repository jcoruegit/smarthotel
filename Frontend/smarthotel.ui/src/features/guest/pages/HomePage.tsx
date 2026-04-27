import { useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/httpClient';
import { useAuth } from '../../auth/hooks/useAuth';
import { getAvailability, type AvailabilityResponse, type AvailableRoom } from '../api/availabilityApi';
import { getRoomImage } from '../constants/roomImages';
import { checkoutSelectionStorageKey } from '../constants/reservationStorage';
import type { ReservationRoomSelection } from '../types/reservationSelection';

export function HomePage() {
  const navigate = useNavigate();
  const { session } = useAuth();

  const defaultDates = useMemo(() => {
    const checkIn = new Date();
    checkIn.setDate(checkIn.getDate() + 7);

    const checkOut = new Date(checkIn);
    checkOut.setDate(checkOut.getDate() + 1);

    return {
      checkIn: formatDateInput(checkIn),
      checkOut: formatDateInput(checkOut),
    };
  }, []);

  const [checkIn, setCheckIn] = useState(defaultDates.checkIn);
  const [checkOut, setCheckOut] = useState(defaultDates.checkOut);
  const [guests, setGuests] = useState(2);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [availability, setAvailability] = useState<AvailabilityResponse | null>(null);
  const [showLoginPopup, setShowLoginPopup] = useState(false);

  async function handleConsultAvailability(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (!isDateRangeValid(checkIn, checkOut)) {
      setError('La fecha de check out debe ser posterior a la fecha de check in.');
      return;
    }

    setLoading(true);

    try {
      const response = await getAvailability({
        checkIn,
        checkOut,
        guests,
      });

      setAvailability(response);
    } catch (unknownError) {
      const message = unknownError instanceof ApiError ? unknownError.message : 'No pudimos consultar disponibilidad.';
      setError(message);
      setAvailability(null);
    } finally {
      setLoading(false);
    }

    return;
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
    <main className="page home-page">
      <header className="hero-block">
        <div className="home-top-bar">
          <p className="eyebrow">SmartHotel Platform</p>
          <div className="home-top-actions">
            <span className="top-link top-link-disabled" role="status" aria-label="Acceso personal proximamente">
              Acceso Personal <span className="top-link-badge">Proximamente</span>
            </span>

            <div className="profile-menu">
              <button className="profile-menu-trigger" type="button" aria-label="Opciones de acceso de cliente">
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <path
                    d="M12 12a4.5 4.5 0 1 0-4.5-4.5A4.5 4.5 0 0 0 12 12Zm0 2.25c-4.07 0-7.5 2.13-7.5 4.75v.25A1.75 1.75 0 0 0 6.25 21h11.5a1.75 1.75 0 0 0 1.75-1.75V19c0-2.62-3.43-4.75-7.5-4.75Z"
                    fill="currentColor"
                  />
                </svg>
              </button>
              <div className="profile-menu-content">
                <Link to="/register">Crear cuenta</Link>
                <Link to="/login">Iniciar sesión</Link>
              </div>
            </div>
          </div>
        </div>

        <h1>Operación hotelera y reservas en una sola interfaz</h1>
        <p className="hero-copy">
          Inicia por autenticación y luego escala al flujo de reservas del cliente y al panel interno para personal.
        </p>
      </header>

      <section className="card card-highlight home-filter-card">
        <h2>Filtrar habitaciones</h2>
        <p className="subtle">Seleccioná tus fechas y la cantidad de huéspedes para consultar disponibilidad.</p>

        <form className="availability-grid" onSubmit={handleConsultAvailability}>
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

function isDateRangeValid(checkIn: string, checkOut: string): boolean {
  if (!checkIn || !checkOut) {
    return false;
  }

  return new Date(checkOut).getTime() > new Date(checkIn).getTime();
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
