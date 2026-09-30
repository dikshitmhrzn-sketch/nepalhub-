import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';

import * as authApi from './api';
import type { LoginRequest, RegisterRequest, Session, SessionUser } from './types';

type AuthStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: AuthStatus;
  user: SessionUser | null;
  isAdmin: boolean;
  isStaff: boolean;
  signIn: (request: LoginRequest) => Promise<void>;
  signUp: (request: RegisterRequest) => Promise<void>;
  signOut: () => Promise<void>;
  reload: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<SessionUser | null>(null);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;

    return () => {
      mounted.current = false;
    };
  }, []);

  const applySession = useCallback((session: Session) => {
    if (!mounted.current) {
      return;
    }

    setUser(session.user);
    setStatus(session.authenticated && session.user ? 'authenticated' : 'anonymous');
  }, []);

  const load = useCallback(async () => {
    try {
      applySession(await authApi.getSession());
    } catch {
      // A failed session probe must not lock the visitor out of the public pages.
      if (mounted.current) {
        setUser(null);
        setStatus('anonymous');
      }
    }
  }, [applySession]);

  useEffect(() => {
    void load();
  }, [load]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      isAdmin: user?.roles.includes('admin') ?? false,
      isStaff: user?.roles.some((role) => role === 'admin' || role === 'moderator') ?? false,
      signIn: async (request) => {
        const signedIn = await authApi.login(request);
        if (mounted.current) {
          setUser(signedIn);
          setStatus('authenticated');
        }
      },
      signUp: async (request) => {
        const created = await authApi.register(request);
        if (mounted.current) {
          setUser(created);
          setStatus('authenticated');
        }
      },
      signOut: async () => {
        await authApi.logout();
        if (mounted.current) {
          setUser(null);
          setStatus('anonymous');
        }
      },
      reload: load,
    }),
    [load, status, user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside an <AuthProvider>.');
  }

  return context;
}
