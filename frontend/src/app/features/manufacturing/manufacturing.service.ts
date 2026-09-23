import { queryString } from '../../core/services/query-string';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from '../../core/services/api.service';
import { BillOfMaterials, BillOfMaterialsInput, ManufacturingPage, ProductionOrder, ProductionOrderInput, ProductionRequirement } from './manufacturing.models';

@Injectable({ providedIn: 'root' })
export class ManufacturingService {
  constructor(private readonly api: ApiService) {}

  listBills(parameters: Record<string, string | number | boolean | undefined>): Observable<ManufacturingPage<BillOfMaterials>> {
    return this.api.get<ManufacturingPage<BillOfMaterials>>(`manufacturing/bills-of-materials${this.toQuery(parameters)}`);
  }

  getBill(id: string): Observable<BillOfMaterials> {
    return this.api.get<BillOfMaterials>(`manufacturing/bills-of-materials/${id}`);
  }

  createBill(input: BillOfMaterialsInput): Observable<BillOfMaterials> {
    return this.api.post<BillOfMaterials, BillOfMaterialsInput>('manufacturing/bills-of-materials', input);
  }

  updateBill(id: string, input: BillOfMaterialsInput): Observable<BillOfMaterials> {
    return this.api.put<BillOfMaterials, BillOfMaterialsInput>(`manufacturing/bills-of-materials/${id}`, input);
  }

  listOrders(parameters: Record<string, string | number | boolean | undefined>): Observable<ManufacturingPage<ProductionOrder>> {
    return this.api.get<ManufacturingPage<ProductionOrder>>(`manufacturing/production-orders${this.toQuery(parameters)}`);
  }

  getOrder(id: string): Observable<ProductionOrder> {
    return this.api.get<ProductionOrder>(`manufacturing/production-orders/${id}`);
  }

  createOrder(input: ProductionOrderInput): Observable<ProductionOrder> {
    return this.api.post<ProductionOrder, ProductionOrderInput>('manufacturing/production-orders', input);
  }

  updateOrder(id: string, input: ProductionOrderInput): Observable<ProductionOrder> {
    return this.api.put<ProductionOrder, ProductionOrderInput>(`manufacturing/production-orders/${id}`, input);
  }

  releaseOrder(id: string): Observable<ProductionOrder> {
    return this.api.post<ProductionOrder, object>(`manufacturing/production-orders/${id}/release`, {});
  }

  getRequirements(id: string, actualProducedQuantity: number): Observable<ProductionRequirement[]> {
    return this.api.get<ProductionRequirement[]>(`manufacturing/production-orders/${id}/requirements?actualProducedQuantity=${encodeURIComponent(String(actualProducedQuantity))}`);
  }

  completeOrder(id: string, actualProducedQuantity: number): Observable<ProductionOrder> {
    return this.api.post<ProductionOrder, { actualProducedQuantity: number }>(`manufacturing/production-orders/${id}/complete`, { actualProducedQuantity });
  }

  private toQuery(parameters: Record<string, string | number | boolean | undefined>): string {
    return queryString(parameters);
  }
}
