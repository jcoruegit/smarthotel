import { Link } from 'react-router-dom';

export function UnauthorizedPage() {
  return (
    <main className="page">
      <section className="card centered-card">
        <p className="eyebrow">Acceso denegado</p>
        <h1>No tenés permisos para ver esta seccion</h1>
        <p className="subtle">Si crees que es un error, pedi revision de roles al administrador.</p>
        <Link className="btn btn-primary" to="/">
          Volver al inicio
        </Link>
      </section>
    </main>
  );
}
