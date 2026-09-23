import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { AuthService } from './auth.service';
import { CompanyContextService } from './company-context.service';
import { authInterceptor, unauthorizedInterceptor } from './auth.interceptor';
import { isApiUrl } from './api-url';

describe('API authentication boundary', () => {
  const auth = { accessToken: 'test-token' as string | null, handleUnauthorized: jasmine.createSpy('unauthorized') };
  let http: HttpClient;
  let requests: HttpTestingController;
  beforeEach(() => {
    auth.accessToken = 'test-token';
    auth.handleUnauthorized.calls.reset();
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor, unauthorizedInterceptor])), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: 'https://erp.example/api' },
      { provide: AuthService, useValue: auth },
      { provide: CompanyContextService, useValue: { companyId: 'company-a' } }
    ] });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
  });
  afterEach(() => requests.verify());

  it('never leaks tokens to prefix lookalikes or external URLs', () => {
    for (const url of ['https://erp.example/api-evil', 'https://erp.example.evil/api', '//evil.example/api', 'https://erp.example/api/auth/google']) {
      http.get(url).subscribe();
      const request = requests.expectOne(url);
      expect(request.request.headers.has('Authorization')).toBeFalse();
      request.flush({});
    }
    expect(isApiUrl('/api/customers', '/api')).toBeTrue();
  });
  it('attaches credentials only to the exact API boundary', () => {
    http.get('https://erp.example/api/customers').subscribe();
    const request = requests.expectOne('https://erp.example/api/customers');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    expect(request.request.headers.get('X-Company-Id')).toBe('company-a');
    request.flush({});
  });
  it('does not log out for third-party errors or an obsolete session', () => {
    http.get('https://external.example/').subscribe({ error: () => {} });
    requests.expectOne('https://external.example/').flush({}, { status: 401, statusText: 'Unauthorized' });
    http.get('https://erp.example/api/customers').subscribe({ error: () => {} });
    auth.accessToken = 'new-session';
    requests.expectOne('https://erp.example/api/customers').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.handleUnauthorized).not.toHaveBeenCalled();
  });
  it('clears the current session on an API unauthorized response', () => {
    http.get('https://erp.example/api/customers').subscribe({ error: () => {} });
    requests.expectOne('https://erp.example/api/customers').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.handleUnauthorized).toHaveBeenCalledTimes(1);
  });
});
