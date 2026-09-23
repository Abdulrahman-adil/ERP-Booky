import { PageResult } from '../master-data/master-data.models';

export type InventoryMovementType =
  | 'OpeningBalance'
  | 'StockIn'
  | 'StockOut'
  | 'PositiveAdjustment'
  | 'NegativeAdjustment';

export const inventoryMovementTypes: ReadonlyArray<{ value: InventoryMovementType; label: string }> = [
  { value: 'OpeningBalance', label: 'Opening balance' },
  { value: 'StockIn', label: 'Stock in' },
  { value: 'StockOut', label: 'Stock out' },
  { value: 'PositiveAdjustment', label: 'Positive adjustment' },
  { value: 'NegativeAdjustment', label: 'Negative adjustment' }
];

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  description?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
  countryCode?: string;
  isActive: boolean;
}

export interface StockMovement {
  id: string;
  movementNumber: string;
  movementType: InventoryMovementType;
  productId: string;
  productName: string;
  productSku: string;
  warehouseId: string;
  warehouseName: string;
  warehouseCode: string;
  quantityIn: number;
  quantityOut: number;
  balanceAfter?: number;
  unitOfMeasureName: string;
  occurredAt: string;
  sourceModule: string;
  sourceEventType: string;
  externalReference?: string;
  notes?: string;
  createdByName: string;
  createdAt?: string;
}

export interface StockBalance {
  productId: string;
  productName: string;
  productSku: string;
  warehouseId: string;
  warehouseName: string;
  warehouseCode: string;
  quantityOnHand: number;
  unitOfMeasureName: string;
  inventoryPurpose?: 'RawMaterial' | 'FinishedGood' | 'PackagingMaterial' | 'SemiFinishedGood' | 'Consumable' | 'TradingItem';
  updatedAt: string;
}

export interface InventoryOverview {
  activeWarehouseCount: number;
  stockedProductCount: number;
  movementCount: number;
  recentMovements: StockMovement[];
  quantityByPurpose: InventoryPurposeQuantity[];
}

export interface InventoryPurposeQuantity {
  purpose: string;
  quantityOnHand: number;
  stockedProductCount: number;
}

export interface WarehouseDetails {
  warehouse: Warehouse;
  stockedProducts: StockBalance[];
  recentMovements: StockMovement[];
}

export interface ProductInventory {
  productId: string;
  totalQuantityOnHand: number;
  unitOfMeasureName?: string;
  warehouseBalances: StockBalance[];
  recentMovements: StockMovement[];
}

export type InventoryPageResult<T> = PageResult<T>;
