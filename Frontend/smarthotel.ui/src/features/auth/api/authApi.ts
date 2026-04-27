import { httpRequest } from '../../../shared/api/httpClient';
import type {
  AuthUserRoles,
  ChangePasswordRequest,
  DocumentTypeOption,
  GuestProfile,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  UpdateGuestProfileRequest,
} from '../../../shared/types/auth';

export async function login(payload: LoginRequest): Promise<LoginResponse> {
  return httpRequest<LoginResponse>('/auth/login', {
    method: 'POST',
    body: payload,
  });
}

export async function register(payload: RegisterRequest): Promise<RegisterResponse> {
  return httpRequest<RegisterResponse>('/auth/register', {
    method: 'POST',
    body: payload,
  });
}

export async function getDocumentTypes(): Promise<DocumentTypeOption[]> {
  return httpRequest<DocumentTypeOption[]>('/auth/document-types');
}

export async function getCurrentGuestProfile(accessToken: string): Promise<GuestProfile | null> {
  return httpRequest<GuestProfile | null>('/auth/me/guest-profile', {
    accessToken,
  });
}

export async function updateCurrentGuestProfile(payload: UpdateGuestProfileRequest, accessToken: string): Promise<GuestProfile> {
  return httpRequest<GuestProfile>('/auth/me/guest-profile', {
    method: 'PUT',
    accessToken,
    body: payload,
  });
}

export async function changePassword(payload: ChangePasswordRequest, accessToken: string): Promise<void> {
  return httpRequest<void>('/auth/change-password', {
    method: 'POST',
    accessToken,
    body: payload,
  });
}

export async function listUsers(accessToken: string): Promise<AuthUserRoles[]> {
  return httpRequest<AuthUserRoles[]>('/auth/users', {
    accessToken,
  });
}

export async function updateUserRoles(
  userId: string,
  roles: Array<'Guest' | 'Staff' | 'Admin'>,
  accessToken: string,
): Promise<AuthUserRoles> {
  return httpRequest<AuthUserRoles>(`/auth/users/${userId}/roles`, {
    method: 'PUT',
    accessToken,
    body: { roles },
  });
}
