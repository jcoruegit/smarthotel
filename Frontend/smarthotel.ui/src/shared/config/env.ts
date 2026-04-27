const defaultApiBaseUrl = 'https://localhost:7087';

export const env = {
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || defaultApiBaseUrl,
};
