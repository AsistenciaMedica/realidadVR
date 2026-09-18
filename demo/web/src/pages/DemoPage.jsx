import { Link } from 'react-router-dom';
import Layout from '../components/Layout';
import { MetaQuestVisual } from '../components/GeneratedArt';

export default function DemoPage() {
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="inner-hero">
          <p className="eyebrow">PRUEBA VITAL VR</p>
          <h2>Vive la experiencia.</h2>
        </section>

        <section className="demo-grid">
          <article className="demo-panel windows-panel">
            <h3>DEMO WINDOWS</h3>
            <ol>
              <li>Descargar</li>
              <li>Extraer</li>
              <li>Ejecutar EmergencyVR.exe</li>
              <li>Elegir escenario</li>
            </ol>
            <Link to="/" className="primary-button">DESCARGAR DEMO ↓</Link>
          </article>

          <article className="demo-panel quest-panel">
            <h3>META QUEST 3</h3>
            <div className="quest-visual">
              <MetaQuestVisual />
            </div>
            <p className="quest-state">PRÓXIMAMENTE</p>
          </article>
        </section>

        <div className="status-row demo-status">
          <span>✓ Demo Windows disponible</span>
          <span>○ Contenido médico en revisión</span>
        </div>
      </div>
    </Layout>
  );
}
