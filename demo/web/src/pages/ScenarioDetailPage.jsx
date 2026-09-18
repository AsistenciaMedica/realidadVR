import { Link, useParams } from 'react-router-dom';
import Layout from '../components/Layout';
import { ScenarioArt } from '../components/GeneratedArt';
import { scenarioCards } from '../data/content';

export default function ScenarioDetailPage() {
  const { id } = useParams();
  const scenario = scenarioCards.find((item) => item.id === id) || scenarioCards[0];

  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="scenario-detail">
          <div className="scenario-detail-visual">
            <ScenarioArt variant={id === 'gym' ? 'gym' : id === 'mall' ? 'mall' : id === 'football' ? 'football' : 'dental'} />
          </div>

          <div className="detail-header-row">
            <p className="eyebrow">ESCENARIO / {scenario.name.toUpperCase()}</p>
            <h2>{scenario.name}</h2>
          </div>

          <div className="detail-meta-grid">
            <div><span>Entorno</span><strong>{scenario.name}</strong></div>
            <div><span>Escenarios</span><strong>04</strong></div>
            <div><span>Procedimientos</span><strong>RCP / DEA / Valoración</strong></div>
            <div><span>Estado</span><strong>En revisión</strong></div>
          </div>

          <div className="type-row">
            <span>RCP</span>
            <span>DEA</span>
            <span>Conciencia</span>
            <span>Respiratorio</span>
            <span>Glucosa</span>
          </div>

          <div className="training-flow">
            <h3>FLUJO DE ENTRENAMIENTO</h3>
            <div className="steps-row">
              {['VALORA', 'DECIDE', 'ACTÚA', 'EVALÚA'].map((step, index) => (
                <div key={step} className="step-pill">
                  <span className="step-number">{index + 1}</span>
                  <span>{step}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="detail-actions">
            <Link to="/demo" className="primary-button">PROBAR DEMO</Link>
            <Link to="/scenarios" className="secondary-button">VER OTRO ESCENARIO</Link>
          </div>
        </section>
      </div>
    </Layout>
  );
}
