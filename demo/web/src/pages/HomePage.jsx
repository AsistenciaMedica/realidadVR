import { Link } from 'react-router-dom';
import Layout from '../components/Layout';
import ScenarioPreview from '../components/ScenarioPreview';
import { highlights } from '../data/content';
import useReleaseCatalog from '../data/useReleaseCatalog';

export default function HomePage() {
  const { stats, scenarioCards, error } = useReleaseCatalog();
  return (
    <Layout>
      <div className="container home-page">
        <section className="hero-panel" id="producto">
          <div className="hero-copy">
            <p className="eyebrow">SIMULACIÓN MÉDICA / REALIDAD VIRTUAL</p>
            <h1>
              Entrena hoy.
              <br />
              Salva vidas
              <br />
              <span>mañana.</span>
            </h1>
            <p className="lead">
              Practica la valoración inicial, la respuesta ante emergencias y la toma de decisiones en situaciones cotidianas, dentro de una simulación repetible.
            </p>
            <div className="hero-actions">
              <Link to="/demo" className="primary-button">
                PROBAR DEMO ↗
              </Link>
              <Link to="/scenarios" className="secondary-button">
                ▶ VER ESCENARIOS
              </Link>
            </div>
            <div className="status-row">
              <span>✓ Demo Windows disponible</span>
              <span>○ Contenido médico en revisión</span>
            </div>
          </div>

          <figure className="hero-visual real-preview">
            <ScenarioPreview environment="gym" eager />
            <figcaption>
              <span className="preview-kicker">DENTRO DE VITAL VR</span>
              <strong>Acércate. Escucha. Actúa.</strong>
              <span>Captura de la aplicación en desarrollo · vista de escritorio</span>
            </figcaption>
          </figure>
        </section>

        <section className="highlights-row" aria-label="Beneficios Vital VR">
          {highlights.map((item) => (
            <div key={item.title} className="mini-feature">
              <span className="feature-icon">{item.icon}</span>
              <span>{item.title}</span>
            </div>
          ))}
        </section>

        <section className="stats-band" aria-label="Números clave">
          {error && <p role="status">{error}</p>}
          {stats.map((stat) => (
            <div key={stat.label} className="stat-box">
              <strong>{stat.value}</strong>
              <span>{stat.label}</span>
            </div>
          ))}
          <div className="stat-box stat-note">
            <span>Contenido médico en revisión</span>
          </div>
        </section>

        <section className="home-environments" aria-labelledby="environments-title">
          <div className="environment-heading">
            <div><p className="eyebrow">TRES LUGARES. DISTINTAS DECISIONES.</p><h2 id="environments-title">Así se ve el entrenamiento.</h2></div>
            <p>Explora los espacios y los casos de la primera edición. Imágenes capturadas en la aplicación, sin retoque visual.</p>
          </div>
          <div className="preview-grid">
            {scenarioCards.map((scenario) => (
              <Link key={scenario.id} to={`/scenarios/${scenario.id}`} className="environment-preview-card">
                <ScenarioPreview environment={scenario.id} />
                <span><strong>{scenario.name}</strong><span>{scenario.cases.length} casos · Explorar →</span></span>
              </Link>
            ))}
          </div>
        </section>
      </div>
    </Layout>
  );
}
