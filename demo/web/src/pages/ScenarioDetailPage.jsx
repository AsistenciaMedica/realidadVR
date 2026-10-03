import { Link, useParams } from 'react-router-dom';
import Layout from '../components/Layout';
import { ScenarioArt } from '../components/GeneratedArt';
import useReleaseCatalog from '../data/useReleaseCatalog';

export default function ScenarioDetailPage() {
  const { id } = useParams();
  const { catalog, scenarioCards, error } = useReleaseCatalog();
  const scenario = scenarioCards.find((item) => item.id === id || item.cases.some((entry) => entry.id === id));
  const selectedCase = scenario?.cases.find((entry) => entry.id === id);

  if (!catalog || !scenario) {
    return (
      <Layout>
        <div className="container page-shell narrow-page">
          <h2>{error || (catalog ? 'Este escenario no está incluido en la primera edición.' : 'Cargando escenario…')}</h2>
          <Link to="/scenarios" className="secondary-button">Ver escenarios</Link>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="scenario-detail">
          <div className="scenario-detail-visual">
            <ScenarioArt variant={scenario.id} />
          </div>

          <div className="detail-header-row">
            <p className="eyebrow">ESCENARIO / {scenario.name.toUpperCase()}</p>
            <h2>{selectedCase?.name || scenario.name}</h2>
          </div>

          <div className="detail-meta-grid">
            <div><span>Entorno</span><strong>{scenario.name}</strong></div>
            <div><span>Casos incluidos</span><strong>{String(scenario.cases.length).padStart(2, '0')}</strong></div>
            <div><span>Edición</span><strong>Lanzamiento inicial</strong></div>
            <div><span>Estado</span><strong>En revisión</strong></div>
          </div>

          <div className="type-row">
            {scenario.tags.map((tag) => <span key={tag}>{tag}</span>)}
          </div>

          <section className="included-cases" aria-labelledby="included-cases-title">
            <h3 id="included-cases-title">{scenario.cases.length} casos en {scenario.name}</h3>
            <ol>
              {scenario.cases.map((entry) => (
                <li key={entry.id} aria-current={entry.id === id ? 'true' : undefined}>
                  <h4><Link to={`/scenarios/${entry.id}`}>{entry.name}</Link></h4>
                  {entry.patientIdentity && <p className="case-status">{entry.patientIdentity.displayName} · {entry.patientIdentity.age} años · {entry.patientIdentity.role}</p>}
                  <p>{entry.description}</p>
                  <p className="case-status">{entry.difficulty} · Contenido médico en revisión</p>
                </li>
              ))}
            </ol>
          </section>

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
