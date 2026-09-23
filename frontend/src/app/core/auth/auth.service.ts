import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';

import { ApiService } from '../services/api.service';
import { CompanyContextService } from './company-context.service';
import { CurrentUser, GoogleLoginRequest, LoginCredentials, LoginResponse } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenStorageKey = 'erp.access-token';
  private readonly userSubject = new BehaviorSubject<CurrentUser | null>(null);
  private restoreRequest?: Observable<CurrentUser | null>;
  private sessionVersion = 0;

  readonly user$ = this.userSubject.asObservable();

  constructor(
    private readonly api: ApiService,
    private readonly router: Router,
    private readonly companyContext: CompanyContextService
  ) {}

  get currentUser(): CurrentUser | null {
    return this.userSubject.value;
  }

  get accessToken(): string | null {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage.getItem(this.tokenStorageKey);
  }

  login(credentials: LoginCredentials): Observable<LoginResponse> {
    return this.api.post<LoginResponse, LoginCredentials>('auth/login', credentials).pipe(
      tap((response) => this.establishSession(response))
    );
  }

  loginWithGoogleIdToken(idToken: string): Observable<LoginResponse> {
    return this.api.post<LoginResponse, GoogleLoginRequest>('auth/google', { idToken }).pipe(
      tap((response) => this.establishSession(response))
    );
  }

  restoreSession(): Observable<CurrentUser | null> {
    if (this.currentUser) {
      return of(this.currentUser);
    }

    const accessToken = this.accessToken;
    const sessionVersion = this.sessionVersion;

    if (!accessToken) {
      return of(null);
    }

    if (!this.restoreRequest) {
      this.restoreRequest = this.api.get<CurrentUser>('auth/me').pipe(
        map((user) => this.isCurrentSession(accessToken, sessionVersion) ? user : null),
        tap((user) => {
          if (user) {
            this.userSubject.next(user);
          }
        }),
        catchError(() => {
          this.clearSession();
          return of(null);
        }),
        finalize(() => {
          if (this.sessionVersion === sessionVersion) {
            this.restoreRequest = undefined;
          }
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.restoreRequest;
  }

  logout(): void {
    this.clearSession();
    void this.router.navigate(['/login'], { replaceUrl: true });
  }

  handleUnauthorized(): void {
    this.clearSession();
    void this.router.navigate(['/login'], { replaceUrl: true });
  }

  private establishSession(response: LoginResponse): void {
    sessionStorage.setItem(this.tokenStorageKey, response.accessToken);
    if (this.companyContext.companyId && !response.user.companyIds.includes(this.companyContext.companyId)) {
      this.companyContext.clear();
    }
    this.userSubject.next(response.user);
  }

  private clearSession(): void {
    this.sessionVersion += 1;
    this.restoreRequest = undefined;
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.removeItem(this.tokenStorageKey);
    }

    this.companyContext.clear();
    this.userSubject.next(null);
  }

  private isCurrentSession(accessToken: string, sessionVersion: number): boolean {
    return this.sessionVersion === sessionVersion && this.accessToken === accessToken;
  }
}
