export type User = { username: string; displayName: string };
export type Page = { bodyTemplate: string; css: string };
export type CsrfToken = { token: string };
export type LoginRequest = { email: string; password: string };
export type SignupRequest = LoginRequest & { username: string; displayName: string };
export type EmailRequest = { email: string };
export type ConfirmEmailRequest = EmailRequest & { token: string };
export type ResetPasswordRequest = ConfirmEmailRequest & { password: string };
export type SavePageRequest = Page;
export type AdminUser = {
  id: string;
  userName: string | null;
  displayName: string;
  email: string | null;
  about: string;
  emailConfirmed: boolean;
};
export type CreateAdminUserRequest = {
  userName: string;
  displayName: string;
  email: string;
  about: string;
  emailConfirmed: boolean;
  password: string;
};
export type UpdateAdminUserRequest = Omit<CreateAdminUserRequest, 'password'> & { password?: string };

export function getCsrfToken(): Promise<CsrfToken> {
  return request<CsrfToken>('/auth/csrf');
}

export function signup(data: SignupRequest): Promise<void> {
  return request<void>('/auth/signup', 'POST', data);
}

export function login(data: LoginRequest): Promise<void> {
  return request<void>('/auth/login', 'POST', data);
}

export function logout(): Promise<void> {
  return request<void>('/auth/logout', 'POST');
}

export function confirmEmail(data: ConfirmEmailRequest): Promise<void> {
  return request<void>('/auth/confirm-email', 'POST', data);
}

export function resendConfirmation(data: EmailRequest): Promise<void> {
  return request<void>('/auth/resend-confirmation', 'POST', data);
}

export function forgotPassword(data: EmailRequest): Promise<void> {
  return request<void>('/auth/forgot-password', 'POST', data);
}

export function resetPassword(data: ResetPasswordRequest): Promise<void> {
  return request<void>('/auth/reset-password', 'POST', data);
}

export function getCurrentUser(): Promise<User> {
  return request<User>('/me');
}

export function getMyPage(): Promise<Page> {
  return request<Page>('/me/page');
}

export function saveMyPage(data: SavePageRequest): Promise<Page> {
  return request<Page>('/me/page', 'PUT', data);
}

export function deleteMyPage(): Promise<void> {
  return request<void>('/me/page', 'DELETE');
}

export function getAdminUsers(): Promise<AdminUser[]> {
  return request<AdminUser[]>('/admin/users');
}

export function createAdminUser(data: CreateAdminUserRequest): Promise<void> {
  return request<void>('/admin/users', 'POST', data);
}

export function updateAdminUser(id: string, data: UpdateAdminUserRequest): Promise<void> {
  return request<void>(`/admin/users/${encodeURIComponent(id)}`, 'PUT', data);
}

export function deleteAdminUser(id: string): Promise<void> {
  return request<void>(`/admin/users/${encodeURIComponent(id)}`, 'DELETE');
}

export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
  }
}

async function request<T>(path: string, method: 'GET' | 'POST' | 'PUT' | 'DELETE' = 'GET', body?: unknown): Promise<T> {
  const headers: Record<string, string> = {};
  if (method !== 'GET') {
    const { token } = await getCsrfToken();
    headers['X-CSRF-Token'] = token;
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const response = await fetch(`/api${path}`, {
    method,
    headers,
    credentials: 'same-origin',
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const errors = problem?.errors as Record<string, string[]> | undefined;
    const message = errors ? Object.values(errors).flat().join(' ') : problem?.detail;
    throw new ApiError(message ?? (response.status === 401
      ? 'Your session has ended. Log in again, then retry.'
      : 'The request failed. Please try again.'), response.status);
  }
  return response.status === 204 ? undefined as T : response.json();
}

export function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'The request failed. Please try again.';
}
