export function PricingRulesPage() {
  return (
    <main className="page staff-page">
      <header className="section-header">
        <p className="eyebrow">Staff/Admin</p>
        <h1>Pricing rules</h1>
        <p className="subtle">Base para alta, modificacion y baja de reglas de precio.</p>
      </header>

      <section className="card">
        <h2>Endpoints listos</h2>
        <ul className="bullet-list">
          <li>GET /api/pricing-rules</li>
          <li>GET /api/pricing-rules/{'{id}'}</li>
          <li>POST /api/pricing-rules</li>
          <li>PUT /api/pricing-rules/{'{id}'}</li>
          <li>DELETE /api/pricing-rules/{'{id}'}</li>
        </ul>
      </section>

    </main>
  );
}
