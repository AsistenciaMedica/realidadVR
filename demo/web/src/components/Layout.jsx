import { Link, NavLink } from 'react-router-dom';
import { navigation } from '../data/content';

export default function Layout({ children }) {
  return (
    <>
      <header className="site-header">
        <div className="container header-inner">
          <Link to="/" className="brand-lockup" aria-label="Vital VR home">
            <img src="/branding/vital-vr-lockup.svg" alt="Vital VR" />
          </Link>

          <nav className="main-nav" aria-label="Principal">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
                end={item.to === '/'}
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <Link to="/demo" className="primary-button compact">
            PROBAR DEMO ↗
          </Link>
        </div>
      </header>

      <main className="page-main">{children}</main>

      <footer className="site-footer">
        <div className="container footer-inner">
          <Link to="/" className="brand-lockup footer-brand" aria-label="Vital VR home">
            <img src="/branding/vital-vr-lockup.svg" alt="Vital VR" />
          </Link>
          <p>Entrena hoy. Salva vidas mañana.</p>
          <div className="footer-links">
            <Link to="/#producto">Producto</Link>
            <Link to="/scenarios">Escenarios</Link>
            <Link to="/technology">Tecnología</Link>
            <Link to="/contact">Contacto</Link>
          </div>
        </div>
      </footer>
    </>
  );
}
