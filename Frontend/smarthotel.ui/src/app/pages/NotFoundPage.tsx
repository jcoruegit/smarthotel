import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <main className="page">
      <section className="card centered-card">
        <p className="eyebrow">404</p>
        <h1>Pagina no encontrada</h1>
        <Link className="btn btn-primary" to="/">
          Ir al inicio
        </Link>
      </section>
    </main>
  );
}
