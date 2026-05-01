export type AppRole = 'Guest' | 'Staff' | 'Admin';

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  roles: AppRole[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  roles: AppRole[];
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  documentTypeId: number;
  documentNumber: string;
  birthDate: string;
  email: string;
  password: string;
}

export interface RegisterResponse {
  userId: string;
  email: string;
  roles: AppRole[];
}

export interface DocumentTypeOption {
  id: number;
  name: string;
}

export interface GuestProfile {
  documentTypeId: number;
  documentTypeName: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
  email: string | null;
  phone: string | null;
}

export interface UpdateGuestProfileRequest {
  documentTypeId: number;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
  email: string;
  phone?: string | null;
}

export interface EmployeeSelfProfile {
  documentTypeId: number;
  documentTypeName: string;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
}

export interface UpdateEmployeeSelfProfileRequest {
  documentTypeId: number;
  firstName: string;
  lastName: string;
  documentNumber: string;
  birthDate: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface AuthUserRoles {
  id: string;
  email: string;
  fullName: string | null;
  roles: AppRole[];
}

export interface EmployeeListItem {
  employeeId: number;
  userId: string;
  firstName: string;
  lastName: string;
  documentTypeId: number;
  documentTypeName: string;
  documentNumber: string;
  birthDate: string;
  email: string;
  profile: 'Staff' | 'Admin';
}

export interface ListEmployeesFilters {
  firstName?: string;
  lastName?: string;
  documentTypeId?: number;
  documentNumber?: string;
  profile?: 'Staff' | 'Admin';
}

export interface CreateEmployeeRequest {
  firstName: string;
  lastName: string;
  documentTypeId: number;
  documentNumber: string;
  birthDate: string;
  profile: 'Staff' | 'Admin';
}

export interface UpdateEmployeeRequest {
  firstName: string;
  lastName: string;
  documentTypeId: number;
  documentNumber: string;
  birthDate: string;
  profile: 'Staff' | 'Admin';
}

export interface CreateEmployeeResponse {
  employeeId: number;
  userId: string;
  fullName: string;
  email: string;
  profile: 'Staff' | 'Admin';
  temporaryPassword: string;
}
