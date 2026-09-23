import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { API_BASE_URL } from '../config/api.config';
import { CompanyContextService } from './company-context.service';
import { AuthService } from './auth.service';
import { isApiUrl, isLoginUrl } from './api-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const companyContext = inject(CompanyContextService);
  const apiBaseUrl = inject(API_BASE_URL).replace(/\/+$/, '');
  const accessToken = auth.accessToken;

  if (!accessToken || !isApiUrl(request.url, apiBaseUrl) || isLoginUrl(request.url)) {
    return next(request);
  }

  const companyId = companyContext.companyId;
  const headers: Record<string, string> = { Authorization: `Bearer ${accessToken}` };

  if (companyId) {
    headers['X-Company-Id'] = companyId;
  }

  return next(request.clone({ setHeaders: headers }));
};

export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const apiBaseUrl = inject(API_BASE_URL);
  const sessionToken = auth.accessToken;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse
        && error.status === 401
        && isApiUrl(request.url, apiBaseUrl)
        && !isLoginUrl(request.url)
        && sessionToken !== null && sessionToken === auth.accessToken) {
        auth.handleUnauthorized();
      }

      return throwError(() => error);
    })
  );
};
