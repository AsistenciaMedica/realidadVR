import { Link } from 'react-router-dom';
import Layout from '../components/Layout';
import { ScenarioArt } from '../components/GeneratedArt';
import { scenarioCards } from '../data/content';

const filters = ['Todos', 'RCP', 'DEA', 'Conciencia', 'Glucosa', 'Respiratorio', 'Trauma'];

export default function ScenariosPage() {
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="inner-hero">
          <p className="eyebrow">ESCENARIOS / ENTORNOS REALES</p>
          <h2>Entrena donde las emergencias ocurren.</h2>
          <p className="subhead">
            Cuatro ambientes procedimentales diseñados para practicar decisiones críticas bajo presión.
          </p>
          <div className="filter-row" aria-label="Filtros de escenarios">
            {filters.map((filter, index) => (
              <button key={filter} className={index === 0 ? 'filter-button active' : 'filter-button'} type="button">
                {filter}
              </button>
            ))}
          </div>
        </section>

        <section className="scenario-grid">
          {scenarioCards.map((scenario, index) => (
            <article key={scenario.id} className={`scenario-card ${scenario.accent}`}>
              <div className="scenario-image-wrap">
                <ScenarioArt variant={['dental', 'gym', 'mall', 'football'][index]} />
              </div>
              <div className="scenario-content">
                <h3>{scenario.name}</h3>
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
