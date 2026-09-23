export type MasterDataKind = 'customers' | 'suppliers' | 'products' | 'categories' | 'units';

export interface PageResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface Partner {
  id: string;
  code: string;
  legalName: string;
  taxIdentifier?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
  countryCode?: string;
  email?: string;
  phone?: string;
  paymentTermsDays: number;
  isActive: boolean;
}

export interface Category {
  id: string;
  code: string;
  name: string;
  parentCategoryId?: string;
  parentCategoryName?: string;
  isActive: boolean;
}

export interface Unit {
  id: string;
  code: string;
  name: string;
  dimension: string;
  conversionFactorToBase: number;
  decimalPlaces: number;
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  productType: 'Stock' | 'NonStock' | 'Service';
  stockUnitOfMeasureId?: string;
  unitOfMeasureName?: string;
  productCategoryId?: string;
  productCategoryName?: string;
  inventoryPurpose?: 'RawMaterial' | 'FinishedGood' | 'PackagingMaterial' | 'SemiFinishedGood' | 'Consumable' | 'TradingItem';
  isActive: boolean;
}
