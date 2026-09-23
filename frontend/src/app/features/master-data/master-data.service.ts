import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { MasterDataKind, PageResult } from './master-data.models';

@Injectable({ providedIn: 'root' })
export class MasterDataService {
  constructor(private readonly api: ApiService) {}

  list<T>(kind: MasterDataKind, parameters: Record<string, string | number | boolean | undefined>): Observable<PageResult<T>> {
    const query = Object.entries(parameters)
      .filter(([, value]) => value !== undefined && value !== '')
      .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
      .join('&');
    return this.api.get<PageResult<T>>(`${this.endpoint(kind)}${query ? `?${query}` : ''}`);
  }

  get<T>(kind: MasterDataKind, id: string): Observable<T> {
    return this.api.get<T>(`${this.endpoint(kind)}/${id}`);
  }

  create<T>(kind: MasterDataKind, body: unknown): Observable<T> {
    return this.api.post<T, unknown>(this.endpoint(kind), body);
  }

  update<T>(kind: MasterDataKind, id: string, body: unknown): Observable<T> {
    return this.api.put<T, unknown>(`${this.endpoint(kind)}/${id}`, body);
  }

  setStatus(kind: Exclude<MasterDataKind, 'units'>, id: string, isActive: boolean): Observable<void> {
    return this.api.put<void, { isActive: boolean }>(`${this.endpoint(kind)}/${id}/status`, { isActive });
  }

  deleteSupplier(id: string): Observable<void> {
    return this.api.delete<void>(`${this.endpoint('suppliers')}/${id}`);
  }

  private endpoint(kind: MasterDataKind): string {
    return {
      customers: 'customers',
      suppliers: 'suppliers',
      products: 'products',
      categories: 'product-categories',
      units: 'units-of-measure'
    }[kind];
  }
}
