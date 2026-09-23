import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { ConfirmDialogService } from '../../core/feedback/confirm-dialog.service';
import { FeedbackService } from '../../core/feedback/feedback.service';
import { AdministrationPermission, AdministrationRole, AdministrationUser, DevelopmentResetPreview } from './administration.models';
import { AdministrationService } from './administration.service';

type AdministrationTab = 'users' | 'roles' | 'development';

@Component({
  selector: 'app-administration',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './administration.component.html',
  styleUrl: './administration.component.scss'
})
export class AdministrationComponent implements OnInit {
  activeTab: AdministrationTab = 'users';
  permissions: AdministrationPermission[] = [];
  roles: AdministrationRole[] = [];
  users: AdministrationUser[] = [];
  resetPreview?: DevelopmentResetPreview;
  isLoading = true;
  isSaving = false;
  errorMessage = '';
  selectedUser?: AdministrationUser;
  selectedRole?: AdministrationRole;
  selectedUserRoleIds = new Set<string>();
  selectedPermissionIds = new Set<string>();

  readonly userForm = this.formBuilder.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', Validators.maxLength(200)]
  });
  readonly roleForm = this.formBuilder.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)]
  });
  readonly resetForm = this.formBuilder.group({ confirmation: ['', Validators.required] });

  constructor(
    private readonly administration: AdministrationService,
    private readonly auth: AuthService,
    private readonly confirmation: ConfirmDialogService,
    private readonly feedback: FeedbackService,
    private readonly formBuilder: FormBuilder
  ) {}

  ngOnInit(): void { this.load(); }

  get canManage(): boolean { return this.auth.currentUser?.permissions.includes('users.manage') === true; }
  get canReset(): boolean { return this.auth.currentUser?.permissions.includes('development.reset') === true; }
  get filteredUsers(): AdministrationUser[] {
    return this.users.filter(user => `${user.displayName} ${user.email} ${user.roleNames.join(' ')}`.toLowerCase().includes(this.userSearch.toLowerCase()));
  }
  userSearch = '';

  get permissionGroups(): { label: string; permissions: AdministrationPermission[] }[] {
    const labels: Record<string, string> = {
      customers: 'Customers', products: 'Products, categories & units', inventory: 'Inventory & warehouses', sales: 'Sales',
      payments: 'Customer payments', receivables: 'Receivables', purchases: 'Purchases & suppliers', payables: 'Payables',
      supplierpayments: 'Supplier payments', cash: 'Cash & bank', accounting: 'Accounting', journals: 'Journals',
      chartofaccounts: 'Chart of accounts', accountingconfiguration: 'Accounting setup', documents: 'Attachments',
      reports: 'Reports', financialreports: 'Financial reports & exports', users: 'Administration', development: 'Development tools'
    };
    const groups = new Map<string, AdministrationPermission[]>();
    this.permissions.filter(permission => permission.isActive).forEach(permission => {
      const prefix = permission.key.split('.')[0];
      groups.set(prefix, [...(groups.get(prefix) ?? []), permission]);
    });
    return [...groups.entries()].map(([prefix, permissions]) => ({ label: labels[prefix] ?? prefix, permissions })).sort((left, right) => left.label.localeCompare(right.label));
  }

  selectTab(tab: AdministrationTab): void {
    this.activeTab = tab;
    if (tab === 'development' && this.canReset && !this.resetPreview) this.loadResetPreview();
  }

  startUser(user?: AdministrationUser): void {
    this.selectedUser = user;
    this.selectedUserRoleIds = new Set(user?.roleIds ?? this.roles.filter(role => role.name === 'Administrator').map(role => role.id));
    this.userForm.reset({ displayName: user?.displayName ?? '', email: user?.email ?? '', password: '' });
    this.errorMessage = '';
  }

  startRole(role?: AdministrationRole): void {
    this.selectedRole = role;
    this.selectedPermissionIds = new Set(role?.permissionIds ?? []);
    this.roleForm.reset({ name: role?.name ?? '', description: role?.description ?? '' });
    this.errorMessage = '';
  }

  toggleUserRole(roleId: string): void { this.toggle(this.selectedUserRoleIds, roleId); }
  togglePermission(permissionId: string): void { this.toggle(this.selectedPermissionIds, permissionId); }
  hasUserRole(roleId: string): boolean { return this.selectedUserRoleIds.has(roleId); }
  hasPermission(permissionId: string): boolean { return this.selectedPermissionIds.has(permissionId); }

  saveUser(): void {
    if (!this.canManage || this.userForm.invalid || this.selectedUserRoleIds.size === 0 || this.isSaving) {
      this.userForm.markAllAsTouched();
      this.errorMessage = this.selectedUserRoleIds.size === 0 ? 'Assign at least one active role.' : '';
      return;
    }
    const value = this.userForm.getRawValue();
    const password = value.password?.trim() || null;
    if (!this.selectedUser && !password) { this.errorMessage = 'A temporary password is required for a new user.'; return; }
    this.isSaving = true;
    const request = { displayName: value.displayName!, email: value.email!, roleIds: [...this.selectedUserRoleIds], password };
    const save = this.selectedUser ? this.administration.updateUser(this.selectedUser.id, request) : this.administration.createUser(request);
    save.subscribe({ next: user => { this.upsertUser(user); this.isSaving = false; this.feedback.success(`${user.displayName} ${this.selectedUser ? 'updated' : 'created'}.`); this.startUser(); }, error: error => { this.isSaving = false; this.errorMessage = error.error?.title ?? 'We could not save this user.'; } });
  }

  saveRole(): void {
    if (!this.canManage || this.roleForm.invalid || this.isSaving) { this.roleForm.markAllAsTouched(); return; }
    this.isSaving = true;
    const value = this.roleForm.getRawValue();
    const request = { name: value.name!, description: value.description?.trim() || null, permissionIds: [...this.selectedPermissionIds] };
    const save = this.selectedRole ? this.administration.updateRole(this.selectedRole.id, request) : this.administration.createRole(request);
    save.subscribe({ next: role => { this.upsertRole(role); this.isSaving = false; this.feedback.success(`${role.name} role ${this.selectedRole ? 'updated' : 'created'}.`); this.startRole(); }, error: error => { this.isSaving = false; this.errorMessage = error.error?.title ?? 'We could not save this role.'; } });
  }

  changeUserStatus(user: AdministrationUser): void {
    void this.confirmation.confirm({ title: `${user.isActive ? 'Deactivate' : 'Activate'} ${user.displayName}?`, message: user.isActive ? 'This user will no longer be able to sign in. Their history and assignments remain preserved.' : 'This user will regain access with their assigned roles.', confirmLabel: user.isActive ? 'Deactivate user' : 'Activate user', tone: user.isActive ? 'danger' : 'primary' }).then(confirmed => {
      if (!confirmed) return;
      this.administration.setUserStatus(user.id, !user.isActive).subscribe({ next: () => { user.isActive = !user.isActive; this.feedback.success(`${user.displayName} is now ${user.isActive ? 'active' : 'inactive'}.`); }, error: error => this.feedback.error(error.error?.title ?? 'We could not update this user.') });
    });
  }

  changeRoleStatus(role: AdministrationRole): void {
    void this.confirmation.confirm({ title: `${role.isActive ? 'Deactivate' : 'Activate'} ${role.name}?`, message: role.isActive ? 'Users keep the assignment, but this role grants no access while inactive.' : 'Assigned users receive this role again on their next sign in.', confirmLabel: role.isActive ? 'Deactivate role' : 'Activate role', tone: role.isActive ? 'warning' : 'primary' }).then(confirmed => {
      if (!confirmed) return;
      this.administration.setRoleStatus(role.id, !role.isActive).subscribe({ next: () => { role.isActive = !role.isActive; this.feedback.success(`${role.name} is now ${role.isActive ? 'active' : 'inactive'}.`); }, error: error => this.feedback.error(error.error?.title ?? 'We could not update this role.') });
    });
  }

  resetDevelopmentData(): void {
    if (!this.canReset || this.resetForm.invalid || this.isSaving) { this.resetForm.markAllAsTouched(); return; }
    const confirmation = this.resetForm.getRawValue().confirmation!;
    void this.confirmation.confirm({ title: 'Reset development data?', message: 'This removes all demo transactions, inventory, partners, products, warehouses, attachments, and financial history. It preserves company configuration, access control, chart of accounts, accounting setup, and migration history.', confirmLabel: 'Reset development data', tone: 'danger' }).then(confirmed => {
      if (!confirmed) return;
      this.isSaving = true;
      this.administration.resetDevelopmentData(confirmation).subscribe({ next: () => { this.isSaving = false; this.resetForm.reset(); this.feedback.success('Development data was reset. The ERP is ready for a fresh demonstration.'); this.load(); }, error: error => { this.isSaving = false; this.errorMessage = error.error?.title ?? 'Development data could not be reset.'; } });
    });
  }

  private load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    forkJoin({ permissions: this.administration.permissions(), roles: this.administration.roles(), users: this.administration.users() }).subscribe({ next: result => { this.permissions = result.permissions; this.roles = result.roles; this.users = result.users; this.isLoading = false; this.startUser(); this.startRole(); if (this.activeTab === 'development' && this.canReset) this.loadResetPreview(); }, error: error => { this.isLoading = false; this.errorMessage = error.status === 403 ? 'You do not have permission to administer access.' : 'Administration data could not be loaded.'; } });
  }

  private loadResetPreview(): void { this.administration.resetPreview().subscribe({ next: preview => this.resetPreview = preview, error: error => this.errorMessage = error.status === 404 ? 'Development reset is unavailable outside the development environment.' : (error.error?.title ?? 'Reset status could not be loaded.') }); }
  private toggle(set: Set<string>, id: string): void { set.has(id) ? set.delete(id) : set.add(id); }
  private upsertUser(user: AdministrationUser): void { this.users = [...this.users.filter(item => item.id !== user.id), user].sort((left, right) => left.displayName.localeCompare(right.displayName)); }
  private upsertRole(role: AdministrationRole): void { this.roles = [...this.roles.filter(item => item.id !== role.id), role].sort((left, right) => left.name.localeCompare(right.name)); }
}
