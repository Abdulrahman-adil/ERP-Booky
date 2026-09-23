import { Routes } from '@angular/router';

import { authGuard, loginGuard } from './core/auth/auth.guard';
import { AppShellComponent } from './layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [loginGuard],
    loadComponent: () => import('./features/auth/login/login.component').then((module) => module.LoginComponent)
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then((module) => module.DashboardComponent)
      },
      {
        path: 'customers/:id',
        data: { kind: 'customers' },
        loadComponent: () => import('./features/master-data/master-data-detail.component').then((module) => module.MasterDataDetailComponent)
      },
      {
        path: 'customers',
        data: { kind: 'customers' },
        loadComponent: () => import('./features/master-data/master-data-page.component').then((module) => module.MasterDataPageComponent)
      },
      {
        path: 'suppliers/:id',
        data: { kind: 'suppliers' },
        loadComponent: () => import('./features/master-data/master-data-detail.component').then((module) => module.MasterDataDetailComponent)
      },
      {
        path: 'suppliers',
        data: { kind: 'suppliers' },
        loadComponent: () => import('./features/master-data/master-data-page.component').then((module) => module.MasterDataPageComponent)
      },
      {
        path: 'products/categories',
        data: { kind: 'categories' },
        loadComponent: () => import('./features/master-data/master-data-page.component').then((module) => module.MasterDataPageComponent)
      },
      {
        path: 'products/units',
        data: { kind: 'units' },
        loadComponent: () => import('./features/master-data/master-data-page.component').then((module) => module.MasterDataPageComponent)
      },
      {
        path: 'products/:id',
        data: { kind: 'products' },
        loadComponent: () => import('./features/master-data/master-data-detail.component').then((module) => module.MasterDataDetailComponent)
      },
      {
        path: 'products',
        data: { kind: 'products' },
        loadComponent: () => import('./features/master-data/master-data-page.component').then((module) => module.MasterDataPageComponent)
      },
      {
        path: 'inventory/warehouses/:id',
        loadComponent: () => import('./features/inventory/warehouse-detail.component').then((module) => module.WarehouseDetailComponent)
      },
      {
        path: 'inventory/warehouses',
        loadComponent: () => import('./features/inventory/warehouse-page.component').then((module) => module.WarehousePageComponent)
      },
      {
        path: 'inventory/ledger',
        loadComponent: () => import('./features/inventory/stock-ledger.component').then((module) => module.StockLedgerComponent)
      },
      {
        path: 'inventory',
        loadComponent: () => import('./features/inventory/inventory-overview.component').then((module) => module.InventoryOverviewComponent)
      },
      {
        path: 'manufacturing/bills-of-materials',
        data: { mode: 'boms' },
        loadComponent: () => import('./features/manufacturing/manufacturing-list.component').then((module) => module.ManufacturingListComponent)
      },
      {
        path: 'manufacturing/production-orders/:id',
        loadComponent: () => import('./features/manufacturing/production-order-detail.component').then((module) => module.ProductionOrderDetailComponent)
      },
      {
        path: 'manufacturing/production-orders',
        data: { mode: 'orders' },
        loadComponent: () => import('./features/manufacturing/manufacturing-list.component').then((module) => module.ManufacturingListComponent)
      },
      {
        path: 'sales/:id',
        loadComponent: () => import('./features/sales/sales-invoice-detail.component').then((module) => module.SalesInvoiceDetailComponent)
      },
      {
        path: 'sales',
        loadComponent: () => import('./features/sales/sales-invoice-list.component').then((module) => module.SalesInvoiceListComponent)
      },
      {
        path: 'payments/:id',
        loadComponent: () => import('./features/payments/payment-detail.component').then((module) => module.PaymentDetailComponent)
      },
      {
        path: 'payments',
        loadComponent: () => import('./features/payments/payment-list.component').then((module) => module.PaymentListComponent)
      },
      {
        path: 'receivables',
        loadComponent: () => import('./features/payments/receivables.component').then((module) => module.ReceivablesComponent)
      },
      {
        path: 'purchases/:id',
        loadComponent: () => import('./features/purchases/purchase-bill-detail.component').then((module) => module.PurchaseBillDetailComponent)
      },
      {
        path: 'purchases',
        loadComponent: () => import('./features/purchases/purchase-bill-list.component').then((module) => module.PurchaseBillListComponent)
      },
      {
        path: 'supplier-payments/:id',
        loadComponent: () => import('./features/purchases/supplier-payment-detail.component').then((module) => module.SupplierPaymentDetailComponent)
      },
      {
        path: 'supplier-payments',
        loadComponent: () => import('./features/purchases/supplier-payment-list.component').then((module) => module.SupplierPaymentListComponent)
      },
      {
        path: 'payables',
        loadComponent: () => import('./features/purchases/payables.component').then((module) => module.PayablesComponent)
      },
      {
        path: 'accounting/journals',
        data: { initialView: 'journals' },
        loadComponent: () => import('./features/accounting/accounting-workspace.component').then((module) => module.AccountingWorkspaceComponent)
      },
      {
        path: 'accounting',
        loadComponent: () => import('./features/accounting/accounting-workspace.component').then((module) => module.AccountingWorkspaceComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./features/reports/financial-reports.component').then((module) => module.FinancialReportsComponent)
      },
      {
        path: 'administration',
        loadComponent: () => import('./features/administration/administration.component').then((module) => module.AdministrationComponent)
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
