export interface AdministrationPermission {
  id: string;
  key: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface AdministrationRole {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  permissionIds: string[];
  assignedUserCount: number;
}

export interface AdministrationUser {
  id: string;
  displayName: string;
  email: string;
  isActive: boolean;
  roleIds: string[];
  roleNames: string[];
  passwordChangedAt?: string | null;
}

export interface AdministrationRoleInput {
  name: string;
  description?: string | null;
  permissionIds: string[];
}

export interface AdministrationUserInput {
  displayName: string;
  email: string;
  roleIds: string[];
  password?: string | null;
}

export interface DevelopmentResetPreview {
  salesInvoices: number;
  purchaseBills: number;
  customerPayments: number;
  supplierPayments: number;
  inventoryMovements: number;
  openItems: number;
  journalEntries: number;
  generalLedgerEntries: number;
  pendingAccountingTransactions: number;
  attachments: number;
  customers: number;
  suppliers: number;
  products: number;
  warehouses: number;
}
