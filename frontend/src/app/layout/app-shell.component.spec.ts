import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../core/auth/auth.service';
import { FeedbackService } from '../core/feedback/feedback.service';
import { AppShellComponent } from './app-shell.component';

describe('AppShellComponent', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser: { displayName: 'Test Administrator', roles: ['Administrator'], permissions: ['users.manage'] },
            user$: of({ displayName: 'Test Administrator', roles: ['Administrator'], permissions: ['users.manage'] }),
            logout: jasmine.createSpy('logout')
          }
        },
        { provide: FeedbackService, useValue: { soundEnabled$: of(true), setSoundEnabled: jasmine.createSpy('setSoundEnabled') } }
      ]
    }).compileComponents();
  });

  afterEach(() => localStorage.clear());

  it('keeps an explicit expand control available after collapsing the sidebar', () => {
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const host = fixture.nativeElement as HTMLElement;
    const toggle = host.querySelector<HTMLButtonElement>('.collapse-button')!;

    toggle.click();
    fixture.detectChanges();

    expect(component.isSidebarCollapsed).toBeTrue();
    expect(localStorage.getItem('erp.sidebar-collapsed')).toBe('true');
    expect(host.querySelector<HTMLButtonElement>('.collapse-button')?.getAttribute('aria-label')).toBe('Expand sidebar');
  });

  it('closes the top-navigation menu after a route selection', () => {
    const fixture = TestBed.createComponent(AppShellComponent);
    const component = fixture.componentInstance;
    const group = component.navigationGroups[0];

    component.setNavigationMode('top');
    component.toggleTopNavigationGroup(group);
    expect(component.isTopNavigationGroupOpen(group)).toBeTrue();

    component.closeTopNavigation();
    expect(component.isTopNavigationGroupOpen(group)).toBeFalse();
  });
});
