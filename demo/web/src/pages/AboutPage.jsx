import Layout from '../components/Layout';
import { stats } from '../data/content';

export default function AboutPage() {
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="inner-hero">
          <p className="eyebrow">SOBRE VITAL VR</p>
          <h2>Simulación médica para un mundo más preparado.</h2>
        </section>

        <section className="about-layout">
          <div className="feature-list-grid">
            <div className="feature-item">
              <span className="feature-icon">⍟</span>
              <strong>Entrena</strong>
            </div>
            <div className="feature-item">
              <span className="feature-icon">◎</span>
              <strong>Evalúa</strong>
            </div>
            <div className="feature-item">
              <span className="feature-icon">△</span>
              <strong>Decide</strong>
            </div>
            <div className="feature-item">
              <span className="feature-icon">◈</span>
              <strong>Actúa</strong>
            </div>
            <div className="feature-item">
              <span className="feature-icon">◉</span>
              <strong>Revisa</strong>
            </div>
            <div className="feature-item">
              <span className="feature-icon">↻</span>
              <strong>Repite</strong>
            </div>
          </div>

          <div className="about-metrics">
            {stats.map((stat) => (
              <div key={stat.label} className="stat-box large">
                <strong>{stat.value}</strong>
                <span>{stat.label}</span>
              </div>
            ))}
          </div>
        </section>
      </div>
    </Layout>
  );
}
