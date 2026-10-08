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

/** Mirrors the anonymous answers: the server replies the same whether or not the address is known. */
export interface ResendVerificationRequest {
  email: string;
}

export interface VerifyEmailRequest {
  email: string;
  token: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  password: string;
}

/** Signed-in only: the current password is the credential, the new one the change. */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
