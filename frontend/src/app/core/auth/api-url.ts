export function isApiUrl(requestUrl: string, apiBaseUrl: string, origin = window.location.origin): boolean {
  try {
    const request = new URL(requestUrl, origin);
    const api = new URL(apiBaseUrl, origin);
    const path = api.pathname.replace(/\/+$/, '');
    return request.origin === api.origin && !request.username && !request.password
      && (request.pathname === path || request.pathname.startsWith(`${path}/`));
  } catch {
    return false;
  }
}

export function isLoginUrl(requestUrl: string): boolean {
  try {
    return /\/auth\/(login|google)\/?$/.test(new URL(requestUrl, window.location.origin).pathname);
  } catch {
    return false;
  }
}
