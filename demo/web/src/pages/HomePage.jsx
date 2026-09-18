import { Link } from 'react-router-dom';
import Layout from '../components/Layout';
import { BackgroundScene, HeroMedicalScene } from '../components/GeneratedArt';
import { highlights, stats } from '../data/content';

export default function HomePage() {
  return (
    <Layout>
      <div className="container home-page">
        <section className="hero-panel">
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
              Práctica la valoración inicial, la respuesta ante emergencias y la toma de decisiones en entornos reales y repetibles.
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

          <div className="hero-visual">
            <div className="visor-shell">
              <div className="scene-backdrop">
                <BackgroundScene tone="cyan" />
              </div>
              <HeroMedicalScene />
              <div className="hud-checklist">
                <span>✓ Evaluar conciencia</span>
                <span>○ Comprobar respiración</span>
                <span>○ Valorar constantes</span>
                <span>○ Preparar DEA</span>
              </div>
              <div className="sim-card">
                <span className="sim-card-label">Clínica dental</span>
                <strong>Uno de nuestros 4 ambientes procedurales</strong>
              </div>
            </div>
          </div>
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
      </div>
    </Layout>
  );
}
