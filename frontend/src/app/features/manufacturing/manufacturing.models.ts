export type ProductionOrderStatus = 'Draft' | 'Released' | 'Completed';

export interface BillOfMaterialsComponent {
  id: string;
  lineNumber: number;
  componentProductId: string;
  componentSku: string;
  componentName: string;
  quantity: number;
  unitOfMeasureId: string;
  unitOfMeasureName: string;
}

export interface BillOfMaterials {
  id: string;
  code: string;
  finishedProductId: string;
  finishedProductSku: string;
  finishedProductName: string;
  outputQuantity: number;
  outputUnitOfMeasureId: string;
  outputUnitOfMeasureName: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  components: BillOfMaterialsComponent[];
}

export interface ProductionOrderMaterial {
  id: string;
  lineNumber: number;
  componentProductId: string;
  componentSku: string;
  componentName: string;
  quantityPerBomOutput: number;
  bomOutputQuantity: number;
  unitOfMeasureId: string;
  unitOfMeasureName: string;
}

export interface ProductionRequirement {
  componentProductId: string;
  componentSku: string;
  componentName: string;
  requiredQuantity: number;
  availableQuantity: number;
  unitOfMeasureId: string;
  unitOfMeasureName: string;
  isAvailable: boolean;
}

export interface ProductionOrder {
  id: string;
  productionOrderNumber: string;
  finishedProductId: string;
  finishedProductSku: string;
  finishedProductName: string;
  billOfMaterialsId: string;
  billOfMaterialsCode: string;
  plannedQuantity: number;
  actualProducedQuantity?: number;
  outputUnitOfMeasureId: string;
  outputUnitOfMeasureName: string;
  sourceWarehouseId: string;
  sourceWarehouseCode: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseCode: string;
  destinationWarehouseName: string;
  productionDate: string;
  status: ProductionOrderStatus;
  notes?: string;
  createdByName: string;
  createdAt: string;
  releasedByName?: string;
  releasedAt?: string;
  postedByName?: string;
  postedAt?: string;
  materials: ProductionOrderMaterial[];
}

export interface ManufacturingPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface BillOfMaterialsInput {
  finishedProductId: string;
  outputQuantity: number;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  notes?: string | null;
  isActive: boolean;
  components: Array<{ componentProductId: string; quantity: number }>;
}

export interface ProductionOrderInput {
  finishedProductId: string;
  billOfMaterialsId: string;
  plannedQuantity: number;
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  productionDate: string;
  notes?: string | null;
}
