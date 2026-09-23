import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { API_BASE_URL } from '../config/api.config';
import { ApiService } from '../services/api.service';
import { AuthService } from './auth.service';

@Component({ standalone: true, template: '' })
class RouterTestComponent {}

describe('AuthService', () => {
  let service: AuthService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        ApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: RouterTestComponent }]),
        { provide: API_BASE_URL, useValue: '/api' }
      ]
    });

    service = TestBed.inject(AuthService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    sessionStorage.clear();
  });

  it('stores the access token and current user after a successful login', () => {
    const user = {
      id: '4e1498b8-5d69-4b95-949f-b7d46443b9f4',
      displayName: 'Taylor Admin',
      email: 'admin@example.test',
      roles: ['Administrator'],
      permissions: ['users.manage'],
      companyIds: ['7fd79377-53bf-4547-a2d1-b704073d1e3d']
    };

    service.login({ email: user.email, password: 'A-strong-password' }).subscribe();

    const request = httpTestingController.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    request.flush({
      accessToken: 'signed-token',
      expiresAt: '2026-08-23T18:00:00Z',
      user
    });

    expect(service.accessToken).toBe('signed-token');
    expect(service.currentUser).toEqual(user);
  });

  it('establishes the same session from a Google ID-token response', () => {
    const user = {
      id: '4e1498b8-5d69-4b95-949f-b7d46443b9f4',
      displayName: 'Google Workspace Owner',
      email: 'owner@example.test',
      roles: ['Owner'],
      permissions: ['users.manage'],
      companyIds: ['7fd79377-53bf-4547-a2d1-b704073d1e3d']
    };

    service.loginWithGoogleIdToken('google-id-token').subscribe();

    const request = httpTestingController.expectOne('/api/auth/google');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ idToken: 'google-id-token' });
    request.flush({
      accessToken: 'google-signed-token',
      expiresAt: '2026-09-13T18:00:00Z',
      user
    });

    expect(service.accessToken).toBe('google-signed-token');
    expect(service.currentUser).toEqual(user);
  });

  it('does not restore a user when logout occurs while session restoration is pending', () => {
    sessionStorage.setItem('erp.access-token', 'signed-token');
    service.restoreSession().subscribe();

    const request = httpTestingController.expectOne('/api/auth/me');
    service.logout();
    request.flush({
      id: '4e1498b8-5d69-4b95-949f-b7d46443b9f4',
      displayName: 'Taylor Admin',
      email: 'admin@example.test',
      roles: ['Administrator'],
      permissions: ['users.manage'],
      companyIds: ['7fd79377-53bf-4547-a2d1-b704073d1e3d']
    });

    expect(service.accessToken).toBeNull();
    expect(service.currentUser).toBeNull();
  });
});
