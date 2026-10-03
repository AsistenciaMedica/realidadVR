import { Link } from 'react-router-dom';
import Layout from '../components/Layout';
import { ScenarioArt } from '../components/GeneratedArt';
import useReleaseCatalog from '../data/useReleaseCatalog';

export default function ScenariosPage() {
  const { catalog, scenarioCards, error } = useReleaseCatalog();
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="inner-hero">
          <p className="eyebrow">ESCENARIOS / ENTORNOS REALES</p>
          <h2>Entrena donde las emergencias ocurren.</h2>
          <p className="subhead">
            {catalog ? `${scenarioCards.length} escenarios con ${scenarioCards[0].cases.length} casos cada uno: ${catalog.scenarios.length} casos para practicar valoración, respuesta y decisiones ante emergencias.` : 'Cargando el catálogo de la primera edición…'}
          </p>
          {error && <p role="status">{error}</p>}
        </section>

        <section className="scenario-grid">
          {scenarioCards.map((scenario) => (
            <article key={scenario.id} className={`scenario-card ${scenario.accent}`}>
              <div className="scenario-image-wrap">
                <ScenarioArt variant={scenario.id} />
              </div>
              <div className="scenario-content">
                <h3>{scenario.name}</h3>
                <p>{scenario.cases.length} casos incluidos</p>
                <div className="tag-row">
                  {scenario.tags.map((tag) => (
                    <span key={tag} className="tiny-tag">{tag}</span>
                  ))}
                </div>
                <p>{scenario.description}</p>
                <Link to={`/scenarios/${scenario.id}`} className="secondary-button inline-button">
                  Explorar escenario
                </Link>
              </div>
            </article>
          ))}
        </section>
      </div>
    </Layout>
  );
}
