import { env } from '../../../shared/config/env';

export interface DemoGuestReservationIdentity {
  firstName: string;
  lastName: string;
  documentNumber: string;
}

export function isDemoGuestAccount(email: string | null | undefined): boolean {
  const sessionEmail = email?.trim();
  const configuredDemoGuestEmail = env.demoGuestEmail?.trim();

  if (!sessionEmail || !configuredDemoGuestEmail) {
    return false;
  }

  return sessionEmail.toLowerCase() === configuredDemoGuestEmail.toLowerCase();
}

export function getDemoGuestReservationIdentity(): DemoGuestReservationIdentity {
  const normalizedDocumentNumber = (env.demoGuestDocumentNumber || '')
    .replace(/\D/g, '')
    .slice(0, 8);

  return {
    firstName: env.demoGuestFirstName || 'Usuario',
    lastName: env.demoGuestLastName || 'Prueba',
    documentNumber: normalizedDocumentNumber || '99999999',
  };
}
