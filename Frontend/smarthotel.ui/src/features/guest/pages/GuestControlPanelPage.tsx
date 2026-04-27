import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { checkoutSelectionStorageKey } from '../constants/reservationStorage';

export function GuestControlPanelPage() {
  const navigate = useNavigate();
  const [showMissingReservationPopup, setShowMissingReservationPopup] = useState(false);

  function handleContinuePayment() {
    if (hasReservationSelection()) {
      navigate('/reservas/confirmar');
      return;
    }

    setShowMissingReservationPopup(true);
  }

  function handleRedirectToControlPanel() {
    setShowMissingReservationPopup(false);
    navigate('/guest/panel', { replace: true });
  }

  return (
    <main className="page guest-reservations-page">
      <header className="section-header">
        <p className="eyebrow">Cliente</p>
        <h1>Panel de control</h1>
        <p className="subtle">Administra tu información, reserva habitaciones y continua pagos pendientes.</p>
      </header>

      <section className="grid-cards guest-control-grid">
        <article className="card form-card">
          <h2>Consultar disponibilidad</h2>
          <p>Abre el formulario de consulta para buscar habitaciones disponibles.</p>
          <Link className="btn btn-primary" to="/reservas">
            Ir a consultar disponibilidad
          </Link>
        </article>

        <article className="card form-card">
          <h2>Continuar con el pago</h2>
          <p>Retoma la confirmación de reserva con los datos ya cargados.</p>
          <button className="btn btn-primary" type="button" onClick={handleContinuePayment}>
            Continuar con el pago
          </button>
        </article>

        <article className="card form-card">
          <h2>Modificar datos</h2>
          <p>Actualiza tus datos personales y tu clave de acceso.</p>
          <Link className="btn btn-primary" to="/guest/panel/datos">
            Ir a modificar datos
          </Link>
        </article>

        <article className="card form-card">
          <h2>Ver reservas</h2>
          <p>Consulta todas tus reservas y aplica filtros por fecha.</p>
          <Link className="btn btn-primary" to="/guest/panel/reservas">
            Ir a ver reservas
          </Link>
        </article>
      </section>

      {showMissingReservationPopup ? (
        <div className="app-popup-backdrop" role="dialog" aria-modal="true" aria-labelledby="missing-reservation-title">
          <section className="app-popup">
            <h2 id="missing-reservation-title">No hay reservas cargadas</h2>
            <p>Para continuar con el pago primero debes cargar una reserva.</p>
            <button className="btn btn-primary" type="button" onClick={handleRedirectToControlPanel}>
              Ir al panel de control
            </button>
          </section>
        </div>
      ) : null}
    </main>
  );
}

function hasReservationSelection(): boolean {
  const rawSelection = sessionStorage.getItem(checkoutSelectionStorageKey);
  if (!rawSelection) {
    return false;
  }

  try {
    const parsed = JSON.parse(rawSelection) as Partial<{ checkIn: string; checkOut: string; room: { roomId?: number } }>;
    return Boolean(parsed.checkIn && parsed.checkOut && parsed.room?.roomId);
  } catch {
    return false;
  }
}
