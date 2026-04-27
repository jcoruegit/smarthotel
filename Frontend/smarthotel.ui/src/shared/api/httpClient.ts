import { env } from '../config/env';

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  accessToken?: string;
  signal?: AbortSignal;
}

interface ProblemDetails {
  detail?: string;
  title?: string;
  message?: string;
}

export class ApiError extends Error {
  public readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export async function httpRequest<TResponse>(
  path: string,
  { method = 'GET', body, accessToken, signal }: RequestOptions = {},
): Promise<TResponse> {
  let response: Response;
  try {
    response = await fetch(buildUrl(path), {
      method,
      signal,
      headers: {
        ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError(
      'No se pudo conectar con la API. Verifica que el backend este levantado y la URL/CORS de desarrollo.',
      0,
    );
  }

  if (!response.ok) {
    throw await mapError(response);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

function buildUrl(path: string): string {
  const base = env.apiBaseUrl.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;

  return `${base}${normalizedPath}`;
}

async function mapError(response: Response): Promise<ApiError> {
  try {
    const payload = (await response.json()) as ProblemDetails;
    const message = payload.detail || payload.message || payload.title || 'No pudimos completar la solicitud.';

    return new ApiError(message, response.status);
  } catch {
    return new ApiError('No pudimos completar la solicitud.', response.status);
  }
}
