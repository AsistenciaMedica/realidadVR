import Layout from '../components/Layout';
import { environmentList } from '../data/content';

export default function ContactPage() {
  return (
    <Layout>
      <div className="container page-shell narrow-page">
        <section className="contact-layout">
          <div className="contact-copy">
            <p className="eyebrow">CONTACTO</p>
            <h2>Lleva Vital VR a tu organización.</h2>
            <ul className="contact-list">
              {environmentList.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>

          <form className="contact-form">
            <label>
              Nombre
              <input type="text" name="name" placeholder="Tu nombre" />
            </label>
            <label>
              Organización
              <input type="text" name="organization" placeholder="Organización" />
            </label>
            <label>
              Email
              <input type="email" name="email" placeholder="email@empresa.com" />
            </label>
            <label>
              Teléfono opcional
              <input type="tel" name="phone" placeholder="Teléfono" />
            </label>
            <label>
              Mensaje
              <textarea name="message" rows="5" placeholder="Cuéntanos cómo quieres usar Vital VR" />
            </label>
            <button type="submit" className="primary-button">SOLICITAR DEMOSTRACIÓN ↗</button>
          </form>
        </section>
      </div>
    </Layout>
  );
}
