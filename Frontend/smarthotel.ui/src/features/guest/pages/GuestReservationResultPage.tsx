import { useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { clearReservationFlowStorage, reservationSuccessStorageKey } from '../constants/reservationStorage';
import type { ReservationDetails } from '../api/reservationsApi';

interface ResultNavigationState {
  reservation?: ReservationDetails;
}

export function GuestReservationResultPage() {
  const location = useLocation();
  const navigate = useNavigate();

  const reservation = useMemo(() => {
    const navigationState = (location.state as ResultNavigationState | null) ?? null;
    if (navigationState?.reservation) {
      sessionStorage.setItem(reservationSuccessStorageKey, JSON.stringify(navigationState.reservation));
      return navigationState.reservation;
    }

    const raw = sessionStorage.getItem(reservationSuccessStorageKey);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as ReservationDetails;
    } catch {
      return null;
    }
  }, [location.state]);

  function handleBackToHome() {
    clearReservationFlowStorage();
    navigate('/', { replace: true });
  }

  function handleBackToReservations() {
    clearReservationFlowStorage();
    navigate('/reservas', { replace: true });
  }

  if (!reservation) {
    return (
      <main className="page guest-reservations-page">
        <section className="card centered-card">
          <h2>No encontramos información de reserva</h2>
          <button className="btn btn-primary" type="button" onClick={handleBackToHome}>
            Volver a Inicio
          </button>
        </section>
      </main>
    );
  }

  return (
    <main className="page guest-reservations-page">
      <section className="card centered-card">
        <p className="eyebrow">Reserva confirmada</p>
        <h1>La reserva se realizo correctamente</h1>
        <p>
          Habitacion {reservation.roomNumber} ({reservation.roomTypeName}) del {reservation.checkIn} al {reservation.checkOut}.
        </p>
        <p>
          Total: {reservation.totalPrice} | Pagado: {reservation.totalPaid} | Saldo: {reservation.remainingBalance}
        </p>
        <p>Estado: {translateReservationStatus(reservation.status)}</p>

        <div className="button-row">
          <button className="btn btn-primary" type="button" onClick={handleBackToReservations}>
            Volver a reservas
          </button>
        </div>
      </section>
    </main>
  );
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
