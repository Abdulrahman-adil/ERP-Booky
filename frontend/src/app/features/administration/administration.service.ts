import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { AdministrationPermission, AdministrationRole, AdministrationRoleInput, AdministrationUser, AdministrationUserInput, DevelopmentResetPreview } from './administration.models';

@Injectable({ providedIn: 'root' })
export class AdministrationService {
  constructor(private readonly api: ApiService) {}

  permissions(): Observable<AdministrationPermission[]> { return this.api.get<AdministrationPermission[]>('administration/permissions'); }
  roles(): Observable<AdministrationRole[]> { return this.api.get<AdministrationRole[]>('administration/roles'); }
  users(): Observable<AdministrationUser[]> { return this.api.get<AdministrationUser[]>('administration/users'); }
  createRole(input: AdministrationRoleInput): Observable<AdministrationRole> { return this.api.post<AdministrationRole, AdministrationRoleInput>('administration/roles', input); }
  updateRole(id: string, input: AdministrationRoleInput): Observable<AdministrationRole> { return this.api.put<AdministrationRole, AdministrationRoleInput>(`administration/roles/${id}`, input); }
  setRoleStatus(id: string, isActive: boolean): Observable<void> { return this.api.put<void, { isActive: boolean }>(`administration/roles/${id}/status`, { isActive }); }
  createUser(input: AdministrationUserInput): Observable<AdministrationUser> { return this.api.post<AdministrationUser, AdministrationUserInput>('administration/users', input); }
  updateUser(id: string, input: AdministrationUserInput): Observable<AdministrationUser> { return this.api.put<AdministrationUser, AdministrationUserInput>(`administration/users/${id}`, input); }
  setUserStatus(id: string, isActive: boolean): Observable<void> { return this.api.put<void, { isActive: boolean }>(`administration/users/${id}/status`, { isActive }); }
  resetPreview(): Observable<DevelopmentResetPreview> { return this.api.get<DevelopmentResetPreview>('administration/development-reset'); }
  resetDevelopmentData(confirmation: string): Observable<void> { return this.api.post<void, { confirmation: string }>('administration/development-reset', { confirmation }); }
}
