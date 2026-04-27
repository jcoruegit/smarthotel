import { Navigate, Outlet } from 'react-router-dom';
import type { AppRole } from '../../shared/types/auth';
import { useAuth } from '../../features/auth/hooks/useAuth';

interface ProtectedRouteProps {
  allowedRoles?: AppRole[];
  redirectTo?: string;
}

export function ProtectedRoute({ allowedRoles, redirectTo = '/login' }: ProtectedRouteProps) {
  const { isAuthenticated, hasRole } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to={redirectTo} replace />;
  }

  if (allowedRoles && allowedRoles.length > 0 && !allowedRoles.some((role) => hasRole(role))) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <Outlet />;
}
