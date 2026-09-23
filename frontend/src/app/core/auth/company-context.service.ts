import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class CompanyContextService {
  private readonly storageKey = 'erp.current-company-id';

  get companyId(): string | null {
    if (typeof sessionStorage === 'undefined') {
      return null;
    }

    return sessionStorage.getItem(this.storageKey);
  }

  select(companyId: string): void {
    if (!this.isGuid(companyId) || typeof sessionStorage === 'undefined') {
      return;
    }

    sessionStorage.setItem(this.storageKey, companyId);
  }

  clear(): void {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.removeItem(this.storageKey);
    }
  }

  private isGuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  }
}
