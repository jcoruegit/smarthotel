import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../../features/auth/hooks/useAuth';

export function PublicOnlyRoute() {
  const { isAuthenticated, hasRole } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Outlet />;
  }

  const defaultDestination = hasRole('Staff') || hasRole('Admin') ? '/staff' : '/reservas';
  const destination = resolveReturnTo(location.search) ?? defaultDestination;
  return <Navigate to={destination} replace />;
}

function resolveReturnTo(search: string): string | null {
  const returnTo = new URLSearchParams(search).get('returnTo');

  if (!returnTo || !returnTo.startsWith('/') || returnTo.startsWith('//')) {
    return null;
  }

  return returnTo;
}
