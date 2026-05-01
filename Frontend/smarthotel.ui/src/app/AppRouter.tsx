import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './guards/ProtectedRoute';
import { PublicOnlyRoute } from './guards/PublicOnlyRoute';
import { AppSessionHeader } from '../features/auth/components/AppSessionHeader';
import { ChatWidget } from '../features/chat/components/ChatWidget';
import { LoginPage } from '../features/auth/pages/LoginPage';
import { RegisterPage } from '../features/auth/pages/RegisterPage';
import { StaffAccessPage } from '../features/auth/pages/StaffAccessPage';
import { HomePage } from '../features/guest/pages/HomePage';
import { GuestReservationsPage } from '../features/guest/pages/GuestReservationsPage';
import { GuestReservationCheckoutPage } from '../features/guest/pages/GuestReservationCheckoutPage';
import { GuestReservationResultPage } from '../features/guest/pages/GuestReservationResultPage';
import { GuestControlPanelPage } from '../features/guest/pages/GuestControlPanelPage';
import { GuestProfileEditPage } from '../features/guest/pages/GuestProfileEditPage';
import { GuestMyReservationsPage } from '../features/guest/pages/GuestMyReservationsPage';
import { StaffDashboardPage } from '../features/staff/pages/StaffDashboardPage';
import { PricingRulesPage } from '../features/staff/pages/PricingRulesPage';
import { EmployeesPage } from '../features/staff/pages/EmployeesPage';
import { EmployeeCreatePage } from '../features/staff/pages/EmployeeCreatePage';
import { StaffProfileEditPage } from '../features/staff/pages/StaffProfileEditPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { UnauthorizedPage } from './pages/UnauthorizedPage';

export function AppRouter() {
  return (
    <BrowserRouter>
      <AppSessionHeader />
      <ChatWidget />

      <Routes>
        <Route path="/" element={<HomePage />} />

        <Route element={<PublicOnlyRoute />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/staff/login" element={<StaffAccessPage />} />
        </Route>

        <Route path="/reservas" element={<GuestReservationsPage />} />
        <Route path="/reservas/confirmar" element={<GuestReservationCheckoutPage />} />
        <Route path="/reservas/resultado" element={<GuestReservationResultPage />} />

        <Route element={<ProtectedRoute allowedRoles={['Guest']} redirectTo="/login" />}>
          <Route path="/guest/panel" element={<GuestControlPanelPage />} />
          <Route path="/guest/panel/datos" element={<GuestProfileEditPage />} />
          <Route path="/guest/panel/reservas" element={<GuestMyReservationsPage />} />
        </Route>

        <Route element={<ProtectedRoute allowedRoles={['Staff', 'Admin']} redirectTo="/staff/login" />}>
          <Route path="/staff" element={<StaffDashboardPage />} />
          <Route path="/staff/pricing" element={<PricingRulesPage />} />

          <Route element={<ProtectedRoute allowedRoles={['Staff']} redirectTo="/staff" />}>
            <Route path="/staff/datos" element={<StaffProfileEditPage />} />
          </Route>

          <Route element={<ProtectedRoute allowedRoles={['Admin']} redirectTo="/staff/login" />}>
            <Route path="/staff/empleados" element={<EmployeesPage />} />
            <Route path="/staff/empleados/alta" element={<EmployeeCreatePage />} />
            <Route path="/staff/empleados/:employeeId/modificar" element={<EmployeeCreatePage />} />
          </Route>
        </Route>

        <Route path="/unauthorized" element={<UnauthorizedPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  );
}
