import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/hooks/useAuth';

export function StaffDashboardPage() {
  const { session, hasRole } = useAuth();
  const isAdmin = hasRole('Admin');

  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Panel Interno</p>
        <h1>Control operativo del hotel</h1>
        <p className="subtle">Usuario: {session?.email}</p>
      </header>

      <section className="grid-cards">
        <article className="card">
          <h2>Reservas</h2>
          <p>Alta, modificacion y baja de reservas internas.</p>
          <div className="button-row">
            <Link className="btn btn-primary" to="/staff/reservas">
              Gestionar reservas
            </Link>
          </div>
        </article>

        <article className="card">
          <h2>Pricing rules</h2>
          <p>Crear y actualizar reglas de precio por tipo de habitacion y fecha.</p>
          <div className="button-row">
            <Link className="btn btn-primary" to="/staff/pricing">
              Gestionar pricing
            </Link>
          </div>
        </article>

        {isAdmin ? (
          <article className="card">
            <h2>Empleados</h2>
            <p>Administrar roles de usuarios internos (solo Admin).</p>
            <div className="button-row">
              <Link className="btn btn-primary" to="/staff/empleados">
                Gestionar empleados
              </Link>
            </div>
          </article>
        ) : null}
      </section>
    </main>
  );
}
