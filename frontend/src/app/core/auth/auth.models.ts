export interface LoginCredentials {
  email: string;
  password: string;
}

export interface GoogleLoginRequest {
  idToken: string;
}

export interface CurrentUser {
  id: string;
  displayName: string;
  email: string;
  roles: string[];
  permissions: string[];
  companyIds: string[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: CurrentUser;
}
