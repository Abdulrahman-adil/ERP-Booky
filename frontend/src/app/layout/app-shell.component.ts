import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';

import { AuthService } from '../core/auth/auth.service';
import { FeedbackService } from '../core/feedback/feedback.service';

interface NavigationItem {
  label: string;
  route: string;
  symbol: string;
  permission?: string;
  exact?: boolean;
  excludedPrefixes?: string[];
}

interface NavigationGroup {
  label: string;
  items: NavigationItem[];
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [AsyncPipe, NgFor, NgIf, RouterLink, RouterOutlet],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss'
})
export class AppShellComponent {
  isSidebarCollapsed = this.readSidebarCollapsed();
  isMobileNavigationOpen = false;
  navigationMode: 'sidebar' | 'top' = this.readNavigationMode();
  openTopNavigationGroup = '';
  private currentPath = '/dashboard';
  readonly soundState$ = this.feedback.soundEnabled$.pipe(map(enabled => ({ enabled })));

  readonly navigationGroups: NavigationGroup[] = [
    { label: 'Overview', items: [{ label: 'Dashboard', route: '/dashboard', symbol: 'OV', exact: true }] },
    { label: 'Operations', items: [
      { label: 'Sales', route: '/sales', symbol: 'SA', permission: 'sales.view' },
      { label: 'Purchases', route: '/purchases', symbol: 'PU', permission: 'purchases.view' },
      { label: 'Inventory', route: '/inventory', symbol: 'IN', permission: 'inventory.view', exact: true },
      { label: 'Stock ledger', route: '/inventory/ledger', symbol: 'SL', permission: 'inventory.view' }
    ] },
    { label: 'Relationships', items: [
      { label: 'Customers', route: '/customers', symbol: 'CU', permission: 'customers.view' },
      { label: 'Suppliers', route: '/suppliers', symbol: 'SU', permission: 'purchases.view' },
      { label: 'Products', route: '/products', symbol: 'PR', permission: 'products.view', excludedPrefixes: ['/products/categories', '/products/units'] }
    ] },
    { label: 'Manufacturing', items: [
      { label: 'Production orders', route: '/manufacturing/production-orders', symbol: 'MO', permission: 'manufacturing.view' },
      { label: 'Bills of materials', route: '/manufacturing/bills-of-materials', symbol: 'BM', permission: 'manufacturing.view' }
    ] },
    { label: 'Finance', items: [
      { label: 'Receivables', route: '/receivables', symbol: 'AR', permission: 'receivables.view' },
      { label: 'Customer payments', route: '/payments', symbol: 'CP', permission: 'payments.view' },
      { label: 'Payables', route: '/payables', symbol: 'AP', permission: 'payables.view' },
      { label: 'Supplier payments', route: '/supplier-payments', symbol: 'SP', permission: 'supplierpayments.view' },
      { label: 'Accounting', route: '/accounting', symbol: 'AC', permission: 'accounting.view', exact: true },
      { label: 'Journal entries', route: '/accounting/journals', symbol: 'JV', permission: 'journals.view' },
      { label: 'Reports', route: '/reports', symbol: 'RE', permission: 'financialreports.view' }
    ] },
    { label: 'Configuration', items: [
      { label: 'Categories', route: '/products/categories', symbol: 'CA', permission: 'products.view' },
      { label: 'Units of measure', route: '/products/units', symbol: 'UM', permission: 'products.view' },
      { label: 'Warehouses', route: '/inventory/warehouses', symbol: 'WH', permission: 'inventory.view' }
    ] },
    { label: 'Administration', items: [{ label: 'Users & access', route: '/administration', symbol: 'AD', permission: 'users.manage' }] }
  ];
  readonly expandedGroups = new Set(this.navigationGroups.map(group => group.label));

  constructor(
    readonly auth: AuthService,
    readonly feedback: FeedbackService,
    router: Router
  ) {
    this.currentPath = this.normalizePath(router.url);
    router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)).subscribe(event => this.currentPath = this.normalizePath(event.urlAfterRedirects));
  }

  toggleSidebar(): void {
    this.isSidebarCollapsed = !this.isSidebarCollapsed;
    localStorage.setItem('erp.sidebar-collapsed', String(this.isSidebarCollapsed));
  }

  toggleMobileNavigation(): void {
    this.isMobileNavigationOpen = !this.isMobileNavigationOpen;
  }

  closeMobileNavigation(): void {
    this.isMobileNavigationOpen = false;
  }

  canShow(permission?: string): boolean {
    return !permission || this.auth.currentUser?.permissions.includes(permission) === true;
  }

  canShowGroup(group: NavigationGroup): boolean {
    return group.items.some(item => this.canShow(item.permission));
  }

  isActive(item: NavigationItem): boolean {
    if (item.exact) return this.currentPath === item.route;
    if (item.excludedPrefixes?.some(prefix => this.currentPath === prefix || this.currentPath.startsWith(`${prefix}/`))) return false;
    return this.currentPath === item.route || this.currentPath.startsWith(`${item.route}/`);
  }

  toggleGroup(group: NavigationGroup): void {
    if (this.isSidebarCollapsed) return;
    this.expandedGroups.has(group.label) ? this.expandedGroups.delete(group.label) : this.expandedGroups.add(group.label);
  }

  isGroupExpanded(group: NavigationGroup): boolean {
    return this.isSidebarCollapsed || this.expandedGroups.has(group.label);
  }

  setSoundEnabled(enabled: boolean): void {
    this.feedback.setSoundEnabled(enabled);
  }

  setNavigationMode(mode: 'sidebar' | 'top'): void {
    this.navigationMode = mode;
    localStorage.setItem('erp.navigation-mode', mode);
    this.isMobileNavigationOpen = false;
    this.openTopNavigationGroup = '';
  }

  toggleTopNavigationGroup(group: NavigationGroup): void {
    this.openTopNavigationGroup = this.openTopNavigationGroup === group.label ? '' : group.label;
  }

  isTopNavigationGroupOpen(group: NavigationGroup): boolean {
    return this.openTopNavigationGroup === group.label;
  }

  closeTopNavigation(): void {
    this.openTopNavigationGroup = '';
  }

  initials(displayName: string): string {
    return displayName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  private readNavigationMode(): 'sidebar' | 'top' {
    return localStorage.getItem('erp.navigation-mode') === 'top' ? 'top' : 'sidebar';
  }

  private readSidebarCollapsed(): boolean {
    return localStorage.getItem('erp.sidebar-collapsed') === 'true';
  }

  private normalizePath(url: string): string {
    return url.split('?')[0].split('#')[0] || '/dashboard';
  }
}
