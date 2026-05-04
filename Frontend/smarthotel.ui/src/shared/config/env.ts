const defaultApiBaseUrl = 'https://localhost:7087';
const defaultDemoGuestEmail = 'guest@smarthotel.dev';
const defaultDemoGuestFirstName = 'Usuario';
const defaultDemoGuestLastName = 'Prueba';
const defaultDemoGuestDocumentNumber = '99999999';

export const env = {
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || defaultApiBaseUrl,
  demoGuestEmail: (import.meta.env.VITE_DEMO_GUEST_EMAIL as string | undefined)?.trim() || defaultDemoGuestEmail,
  demoGuestFirstName: (import.meta.env.VITE_DEMO_GUEST_FIRST_NAME as string | undefined)?.trim() || defaultDemoGuestFirstName,
  demoGuestLastName: (import.meta.env.VITE_DEMO_GUEST_LAST_NAME as string | undefined)?.trim() || defaultDemoGuestLastName,
  demoGuestDocumentNumber:
    (import.meta.env.VITE_DEMO_GUEST_DOCUMENT_NUMBER as string | undefined)?.trim() || defaultDemoGuestDocumentNumber,
};
