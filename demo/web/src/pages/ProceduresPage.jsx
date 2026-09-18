import Layout from '../components/Layout';
import { procedureCards } from '../data/content';

export default function ProceduresPage() {
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="inner-hero">
          <p className="eyebrow">PROCEDIMIENTOS MÉDICOS</p>
          <h2>Practica habilidades que salvan vidas.</h2>
          <p className="subhead">Entrenamiento orientado a la identificación, respuesta y evaluación de emergencias.</p>
        </section>

        <section className="procedures-grid">
          {procedureCards.map((item) => (
            <article key={item.name} className="procedure-card">
              <div className="procedure-icon">{item.icon}</div>
              <h3>{item.name}</h3>
              <p>{item.description}</p>
              <button type="button" className="text-link">{item.cta}</button>
            </article>
          ))}
        </section>
      </div>
    </Layout>
  );
}
