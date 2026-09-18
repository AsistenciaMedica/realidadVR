import Layout from '../components/Layout';
import { MetaQuestVisual } from '../components/GeneratedArt';
import { technologyFeatures } from '../data/content';

export default function TechnologyPage() {
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="technology-hero-wrap">
          <div className="technology-copy">
            <p className="eyebrow">TECNOLOGÍA PARA LA FORMACIÓN</p>
            <h2>Simulación que responde a tus decisiones.</h2>
            <p className="subhead">Tecnología inmersiva, entrenamiento contextual y revisión posterior para mejorar respuestas reales.</p>
          </div>
          <div className="technology-illustration">
            <MetaQuestVisual />
          </div>
        </section>

        <section className="feature-grid-6">
          {technologyFeatures.map(([title, description]) => (
            <article key={title} className="feature-tile">
              <div className="feature-icon-square">◈</div>
              <h3>{title}</h3>
              <p>{description}</p>
            </article>
          ))}
        </section>
      </div>
    </Layout>
  );
}
