/** Mirrors `SessionUserResponse` from the ASP.NET Core API. */
export interface SessionUser {
  id: string;
  email: string;
  fullName: string;
  avatarUrl: string | null;
  isEmailConfirmed: boolean;
  level: number;
  xp: number;
  roles: string[];
}

export interface Session {
  authenticated: boolean;
  user: SessionUser | null;
}

export interface Credentials {
  email: string;
  password: string;
}

export interface LoginRequest extends Credentials {
  rememberMe: boolean;
}

export interface RegisterRequest extends Credentials {
  fullName: string;
  phoneNumber: string | null;
}

export interface CsrfToken {
  headerName: string;
  token: string;
}
