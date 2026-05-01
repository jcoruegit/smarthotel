import { httpRequest } from '../../../shared/api/httpClient';
import type {
  AuthUserRoles,
  ChangePasswordRequest,
  CreateEmployeeRequest,
  CreateEmployeeResponse,
  DocumentTypeOption,
  GuestProfile,
  ListEmployeesFilters,
  EmployeeListItem,
  EmployeeSelfProfile,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  RegisterResponse,
  UpdateEmployeeRequest,
  UpdateEmployeeSelfProfileRequest,
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

export async function getCurrentEmployeeProfile(accessToken: string): Promise<EmployeeSelfProfile> {
  return httpRequest<EmployeeSelfProfile>('/auth/me/employee-profile', {
    accessToken,
  });
}

export async function updateCurrentEmployeeProfile(
  payload: UpdateEmployeeSelfProfileRequest,
  accessToken: string,
): Promise<EmployeeSelfProfile> {
  return httpRequest<EmployeeSelfProfile>('/auth/me/employee-profile', {
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

export async function listEmployees(filters: ListEmployeesFilters, accessToken: string): Promise<EmployeeListItem[]> {
  const query = new URLSearchParams();

  if (filters.firstName) {
    query.set('firstName', filters.firstName);
  }

  if (filters.lastName) {
    query.set('lastName', filters.lastName);
  }

  if (typeof filters.documentTypeId === 'number' && filters.documentTypeId > 0) {
    query.set('documentTypeId', String(filters.documentTypeId));
  }

  if (filters.documentNumber) {
    query.set('documentNumber', filters.documentNumber);
  }

  if (filters.profile) {
    query.set('profile', filters.profile);
  }

  const suffix = query.toString();
  const path = suffix.length > 0 ? `/auth/employees?${suffix}` : '/auth/employees';
  return httpRequest<EmployeeListItem[]>(path, {
    accessToken,
  });
}

export async function createEmployee(payload: CreateEmployeeRequest, accessToken: string): Promise<CreateEmployeeResponse> {
  return httpRequest<CreateEmployeeResponse>('/auth/employees', {
    method: 'POST',
    accessToken,
    body: payload,
  });
}

export async function getEmployeeById(employeeId: number, accessToken: string): Promise<EmployeeListItem> {
  return httpRequest<EmployeeListItem>(`/auth/employees/${employeeId}`, {
    accessToken,
  });
}

export async function updateEmployee(employeeId: number, payload: UpdateEmployeeRequest, accessToken: string): Promise<EmployeeListItem> {
  return httpRequest<EmployeeListItem>(`/auth/employees/${employeeId}`, {
    method: 'PUT',
    accessToken,
    body: payload,
  });
}

export async function deleteEmployee(employeeId: number, accessToken: string): Promise<void> {
  return httpRequest<void>(`/auth/employees/${employeeId}`, {
    method: 'DELETE',
    accessToken,
  });
}
