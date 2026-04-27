import { createContext, useCallback, useMemo, useState, type ReactNode } from 'react';
import { login as loginRequest, register as registerRequest } from '../api/authApi';
import type { AuthSession, LoginRequest, RegisterRequest, AppRole } from '../../../shared/types/auth';
import { clearReservationFlowStorage } from '../../guest/constants/reservationStorage';

const storageKey = 'smarthotel.session';

export interface AuthContextValue {
  session: AuthSession | null;
  isAuthenticated: boolean;
  login: (payload: LoginRequest) => Promise<AuthSession>;
  register: (payload: RegisterRequest) => Promise<void>;
  logout: () => void;
  updateSessionEmail: (email: string) => void;
  hasRole: (role: AppRole) => boolean;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [session, setSession] = useState<AuthSession | null>(() => readSessionFromStorage());

  const login = useCallback(async (payload: LoginRequest) => {
    const response = await loginRequest(payload);

    const authSession: AuthSession = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      userId: response.userId,
      email: response.email,
      roles: response.roles,
    };

    setSession(authSession);
    writeSessionToStorage(authSession);

    return authSession;
  }, []);

  const register = useCallback(async (payload: RegisterRequest) => {
    await registerRequest(payload);
  }, []);

  const logout = useCallback(() => {
    setSession(null);
    clearSessionStorage();
    clearReservationFlowStorage();
  }, []);

  const updateSessionEmail = useCallback((email: string) => {
    setSession((currentSession) => {
      if (!currentSession) {
        return currentSession;
      }

      const updatedSession = {
        ...currentSession,
        email,
      };

      writeSessionToStorage(updatedSession);
      return updatedSession;
    });
  }, []);

  const hasRole = useCallback(
    (role: AppRole) => {
      if (!session) {
        return false;
      }

      return session.roles.includes(role);
    },
    [session],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isAuthenticated: Boolean(session),
      login,
      register,
      logout,
      updateSessionEmail,
      hasRole,
    }),
    [session, login, register, logout, updateSessionEmail, hasRole],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function readSessionFromStorage(): AuthSession | null {
  const raw = sessionStorage.getItem(storageKey);

  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as AuthSession;

    if (!isValidSession(parsed) || isExpired(parsed.expiresAtUtc)) {
      clearSessionStorage();
      return null;
    }

    return parsed;
  } catch {
    clearSessionStorage();
    return null;
  }
}

function writeSessionToStorage(session: AuthSession): void {
  sessionStorage.setItem(storageKey, JSON.stringify(session));
}

function clearSessionStorage(): void {
  sessionStorage.removeItem(storageKey);
  localStorage.removeItem(storageKey);
}

function isValidSession(value: AuthSession | null): value is AuthSession {
  if (!value) {
    return false;
  }

  return Boolean(value.accessToken && value.userId && value.email && value.expiresAtUtc && Array.isArray(value.roles));
}

function isExpired(expiresAtUtc: string): boolean {
  const expiration = Date.parse(expiresAtUtc);

  if (Number.isNaN(expiration)) {
    return true;
  }

  return expiration <= Date.now();
}
