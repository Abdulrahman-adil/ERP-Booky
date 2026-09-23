import { queryString } from '../../core/services/query-string';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { InventoryOverview, InventoryMovementType, InventoryPageResult, ProductInventory, StockMovement, Warehouse, WarehouseDetails } from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  constructor(private readonly api: ApiService) {}

  getOverview(): Observable<InventoryOverview> {
    return this.api.get<InventoryOverview>('inventory/overview');
  }

  listWarehouses(parameters: Record<string, string | number | boolean | undefined>): Observable<InventoryPageResult<Warehouse>> {
    return this.api.get<InventoryPageResult<Warehouse>>(`warehouses${this.toQuery(parameters)}`);
  }

  getWarehouse(id: string): Observable<Warehouse> {
    return this.api.get<Warehouse>(`warehouses/${id}`);
  }

  getWarehouseDetails(id: string): Observable<WarehouseDetails> {
    return this.api.get<WarehouseDetails>(`warehouses/${id}/details`);
  }

  createWarehouse(body: WarehouseInput): Observable<Warehouse> {
    return this.api.post<Warehouse, WarehouseInput>('warehouses', body);
  }

  updateWarehouse(id: string, body: WarehouseInput): Observable<Warehouse> {
    return this.api.put<Warehouse, WarehouseInput>(`warehouses/${id}`, body);
  }

  setWarehouseStatus(id: string, isActive: boolean): Observable<void> {
    return this.api.put<void, { isActive: boolean }>(`warehouses/${id}/status`, { isActive });
  }

  getLedger(parameters: Record<string, string | number | boolean | undefined>): Observable<InventoryPageResult<StockMovement>> {
    return this.api.get<InventoryPageResult<StockMovement>>(`inventory/ledger${this.toQuery(parameters)}`);
  }

  recordMovement(body: InventoryMovementInput): Observable<StockMovement> {
    return this.api.post<StockMovement, InventoryMovementInput>('inventory/movements', body);
  }

  getProductInventory(productId: string): Observable<ProductInventory> {
    return this.api.get<ProductInventory>(`inventory/products/${productId}`);
  }

  private toQuery(parameters: Record<string, string | number | boolean | undefined>): string {
    return queryString(parameters);
  }
}

export interface WarehouseInput {
  name: string;
  description?: string | null;
  addressLine1?: string | null;
  countryCode?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  postalCode?: string | null;
}

export interface InventoryMovementInput {
  productId: string;
  warehouseId: string;
  movementType: InventoryMovementType;
  quantity: number;
  occurredAt?: string | null;
  externalReference?: string | null;
  notes?: string | null;
}
