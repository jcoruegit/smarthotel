import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export function AppSessionHeader() {
  const location = useLocation();
  const navigate = useNavigate();
  const { session, hasRole, logout } = useAuth();
  const [isScrolled, setIsScrolled] = useState(false);

  const isAuthenticated = Boolean(session);
  const showHeader = isAuthenticated && location.pathname !== '/';
  const panelPath = hasRole('Staff') || hasRole('Admin') ? '/staff' : '/guest/panel';

  useEffect(() => {
    if (!showHeader) {
      setIsScrolled(false);
      return;
    }

    const handleScroll = () => {
      setIsScrolled(window.scrollY > 8);
    };

    handleScroll();
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => {
      window.removeEventListener('scroll', handleScroll);
    };
  }, [showHeader, location.pathname]);

  useEffect(() => {
    if (!showHeader) {
      return;
    }

    document.body.classList.add('has-app-session-header');
    return () => {
      document.body.classList.remove('has-app-session-header');
    };
  }, [showHeader]);

  if (!showHeader || typeof document === 'undefined') {
    return null;
  }

  function handleLogout() {
    logout();
    navigate('/', { replace: true });
  }

  return createPortal(
    <header className={`app-session-header ${isScrolled ? 'is-scrolled' : ''}`} aria-label="Header de sesión">
      <div className="app-session-header-inner">
        <Link className="app-session-brand" to={panelPath}>
          SmartHotel Platform
        </Link>

        <div className="app-session-menu">
          <button className="app-session-trigger" type="button" aria-label="Opciones de sesión">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 3.5v7" />
              <path d="M6.2 6.8a7.5 7.5 0 1 0 11.6 0" />
            </svg>
          </button>

          <div className="app-session-dropdown">
            <Link to={panelPath}>Panel de control</Link>
            <button type="button" onClick={handleLogout}>
              Cerrar sesión
            </button>
          </div>
        </div>
      </div>
    </header>,
    document.body,
  );
}
