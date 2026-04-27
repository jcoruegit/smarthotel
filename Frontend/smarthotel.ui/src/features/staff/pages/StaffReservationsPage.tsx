export function StaffReservationsPage() {
  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Staff</p>
        <h1>Gestion de reservas</h1>
        <p className="subtle">Siguiente paso: tabla con filtros + modal de alta/edicion y acciones de cancelacion por rol.</p>
      </header>

      <section className="card">
        <h2>Endpoints listos en backend</h2>
        <ul className="bullet-list">
          <li>POST /api/reservations</li>
          <li>GET /api/reservations/{'{id}'}</li>
          <li>PUT /api/reservations/{'{id}'} (Staff/Admin)</li>
          <li>DELETE /api/reservations/{'{id}'} (Admin)</li>
          <li>POST /api/reservations/{'{id}'}/payments</li>
        </ul>
      </section>

    </main>
  );
}
