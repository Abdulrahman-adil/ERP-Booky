# RABT ERP Project — Technical Audit Report

**Date:** 2026-09-24  
**Branch:** `main` (commit bd2155f — "Prepare API for deployment")  
**Scope:** Complete codebase analysis — no modifications made

---

## 1. PROJECT OVERVIEW

### Architecture & Tech Stack

| Layer | Technology | Version |
|-------|------------|---------|
| **Backend** | .NET 8 (ASP.NET Core) | 8.0 |
| **Frontend** | Angular | 20.3.31 |
| **Database** | PostgreSQL | 16 (Docker) |
| **ORM** | Entity Framework Core | 8.x |
| **Auth** | JWT (HS256) + Google OAuth 2.0 | Custom + Google.Apis.Auth |
| **Testing** | xUnit + EF Core InMemory / Npgsql | — |
| **Containerization** | Docker Compose (dev + prod) | — |

### Solution Structure (Clean Architecture)

```
Erp.sln
├── src/
│   ├── Erp.Api              # Controllers, middleware, auth wiring, Swagger
│   ├── Erp.Application      # Contracts (DTOs), service interfaces, permissions
│   ├── Erp.Domain           # Aggregates, value objects, domain logic
│   ├── Erp.Infrastructure   # EF Core, services, auth stores, migrations
│   └── Erp.SharedKernel     # Common primitives (Money, Quantity, etc.)
├── tests/
│   ├── Erp.Api.IntegrationTests   # 41 tests — full HTTP pipeline
│   ├── Erp.Domain.Tests           # 17 tests — domain invariants
│   └── Erp.Infrastructure.Tests   # 7 tests — persistence model
└── frontend/                # Angular 20 standalone components
```

### Database & EF Core

- **Schema:** `erp` (single schema, company-scoped via `CompanyId` on every entity)
- **Migrations:** 16 migrations applied (Initial → GoogleExternalIdentity)
- **Sequences:** 13 PostgreSQL sequences for document numbering
- **Multi-company:** Row-level isolation via `CompanyId` foreign keys + `CompanyScopedControllerBase`
- **Key tables:** Organizations → Companies → (Users, Roles, Permissions, BusinessPartners, Products, Warehouses, Inventory, Sales, Purchasing, Payments, Accounting, Manufacturing)

### Authentication / Authorization

| Feature | Status | Notes |
|---------|--------|-------|
| Local email/password | ✅ Working | BCrypt hashing, JWT access tokens (configurable expiry) |
| Google OAuth (ID token) | ✅ Working | `GoogleJsonWebSignature.ValidateAsync`; auto-provisions workspace on first sign-in |
| JWT in sessionStorage | ✅ Working | Short-lived, cleared on browser close |
| Role-based permissions | ✅ Working | Company-scoped roles; `ErpPermissions` defines ~50 granular keys |
| Company/workspace isolation | ✅ Working | Every query filtered by `CompanyId` from JWT claims |
| DevelopmentAdmin seeder | ✅ Working | **DEV ONLY** — seeds org, company, admin role, all permissions on startup |
| ExternalWorkspace provisioner | ✅ Working | Google sign-in → creates Organization + Company + Chart of Accounts + Posting Profile + Bank account automatically |
| Password reset / invitation | ❌ Not implemented | No forgot-password flow, no user invitation emails |

**Production vs Development distinction:**
- `DevelopmentIdentitySeeder` runs **only** in `Development` environment
- Requires `DevelopmentAdmin__Email` + `DevelopmentAdmin__Password` env vars
- Creates `DEV` organization/company with full `Administrator` role
- Google OAuth auto-provisioning runs in **all** environments (production-ready)

---

## 2. CURRENTLY IMPLEMENTED FEATURES

### Authentication
| Feature | Status | Explanation |
|---------|--------|-------------|
| Local login | ✅ Working | Email/password → JWT; rate-limited |
| Google login | ✅ Working | ID token validation; auto-provisions isolated workspace |
| User session restore | ✅ Working | `auth/me` endpoint validates token, rebuilds permissions |
| Logout | ✅ Working | Clears sessionStorage + company context |
| Password setup/reset | ❌ Not implemented | No forgot/reset flow, no email delivery |
| User invitation | ❌ Not implemented | Admin must manually create users in DB or via API |

### Users / Roles / Permissions
| Feature | Status | Explanation |
|---------|--------|-------------|
| User management | ✅ Working | CRUD via Administration page; company-scoped role assignments |
| Role CRUD | ✅ Working | Create/update/deactivate roles; grant/replace permissions |
| Permission matrix | ✅ Working | ~50 granular permissions (e.g., `sales.manage`, `journals.post`, `accountingconfiguration.manage`) |
| Owner role (Google) | ✅ Working | Auto-granted all `WorkspaceOwner` permissions on first sign-in |
| Development admin | ✅ Working | Seeded with all permissions in DEV environment only |

### Customers
| Feature | Status | Explanation |
|---------|--------|-------------|
| CRUD | ✅ Working | Legal name, address, tax ID, payment terms, email/phone |
| Auto-numbering | ✅ Working | Sequence `customer_code_sequence` |
| Customer profile | ✅ Working | Links BusinessPartner → CustomerProfile (receivable account, payment terms) |
| Sales history | ✅ Working | `CustomerSalesSummaryDto` — posted totals, outstanding, recent invoices |
| Deactivate | ✅ Working | Soft delete; preserves history |

### Suppliers
| Feature | Status | Explanation |
|---------|--------|-------------|
| CRUD | ✅ Working | Same fields as customers + supplier profile |
| Auto-numbering | ✅ Working | Sequence `supplier_code_sequence` |
| Payables tracking | ✅ Working | `PayablesComponent` — open payables, overdue, aging |
| Delete (no history) | ✅ Working | Hard delete only if zero commercial history; otherwise deactivate |

### Products
| Feature | Status | Explanation |
|---------|--------|-------------|
| CRUD | ✅ Working | Stock/Service types; SKU auto-sequence; category, UoM, inventory purpose |
| Stock tracking | ✅ Working | `IsStockTracked` flag; requires UoM; validates on sales/purchase posting |
| Categories | ✅ Working | Hierarchical (parentCategoryId); CRUD + tree display |
| Units of Measure | ✅ Working | Code, name, dimension, conversion factor, decimals |
| Deactivate | ✅ Working | Soft delete; preserves transaction history |

### Warehouses
| Feature | Status | Explanation |
|---------|--------|-------------|
| CRUD | ✅ Working | Code auto-sequence; address, active flag |
| Stock balances | ✅ Working | Per-product per-warehouse `InventoryBalance` |
| Warehouse detail | ✅ Working | Balances + recent movements tab |

### Inventory
| Feature | Status | Explanation |
|---------|--------|-------------|
| Stock ledger | ✅ Working | Full movement history with filters (product, warehouse, type, date, search) |
| Movement types | ✅ Working | OpeningBalance, StockIn, StockOut, Positive/NegativeAdjustment, ProductionConsumption, ProductionReceipt |
| Inventory overview | ✅ Working | KPIs: active warehouses, stocked products, movement count, purpose breakdown |
| Negative stock prevention | ✅ Working | Serializable transactions + advisory locks; rejects movements causing negative balance |
| Cost tracking | 🟡 Partial | `costAmount` captured on system receipts (PurchaseInvoicePosted); no average/FIFO/LIFO costing yet |

### Sales
| Feature | Status | Explanation |
|---------|--------|-------------|
| Invoice CRUD | ✅ Working | Draft → Post workflow; lines with product, qty, price, discount % |
| Auto-numbering | ✅ Working | `SI-YYYY-NNNNNN` via sequence |
| Posting | ✅ Working | Validates stock, customer, warehouse; creates InventoryTransaction (StockOut) + OpenItem (Receivable) + AccountingTransaction |
| Payment status | ✅ Working | Unpaid / PartiallyPaid / Paid computed from OpenItem allocations |
| Customer summary | ✅ Working | Dashboard widget + dedicated endpoint |

### Purchases
| Feature | Status | Explanation |
|---------|--------|-------------|
| Bill CRUD | ✅ Working | Draft → Post; lines with product, qty, price |
| Auto-numbering | ✅ Working | `PI-YYYY-NNNNNN` |
| Posting | ✅ Working | Creates InventoryTransaction (StockIn) + OpenItem (Payable) + AccountingTransaction |
| Payables tracking | ✅ Working | Aging, overdue flags, supplier payment allocation |
| Supplier payments | ✅ Working | Outgoing payments allocated to payables; creates AccountingTransaction |

### Payments
| Feature | Status | Explanation |
|---------|--------|-------------|
| Customer payments (incoming) | ✅ Working | Allocate to open receivables; full allocation required before post |
| Supplier payments (outgoing) | ✅ Working | Allocate to open payables |
| Cash/Bank accounts | ✅ Working | CRUD; optional GL posting account mapping |
| Payment numbering | ✅ Working | `CP-YYYY-NNNNNN` / `SP-YYYY-NNNNNN` |
| Receivables/Payables lists | ✅ Working | Filterable, pageable, aging, overdue |

### Manufacturing
| Feature | Status | Explanation |
|---------|--------|-------------|
| Bill of Materials | ✅ Working | Header (finished product, output qty, effective dates) + components (product, qty, UoM) |
| Production Orders | ✅ Working | Draft → Release → Complete; material requirements from BOM; source/dest warehouses |
| Stock movements on complete | ✅ Working | Consumes from source (ProductionConsumption), produces to dest (ProductionReceipt) |
| Requirements check | ✅ Working | `GetRequirementsAsync` shows available vs required per component |
| Cost accounting | ❌ Not implemented | No BOM costing, no WIP accounts, no variance analysis |

### Accounting (Core)
| Feature | Status | Explanation |
|---------|--------|-------------|
| Chart of Accounts | ✅ Working | Hierarchical (parent/child); account types (Asset, Liability, Equity, Revenue, Expense); roles (Cash, Bank, AR, AP, Inventory, COGS, etc.); contra accounts via `NormalBalanceOverride` |
| Financial Classes | ✅ Working | Segment dimension (department, project, etc.) |
| Accounting Periods | ✅ Working | Create, open/close; prevents posting in closed periods |
| Posting Profiles | ✅ Working | Maps 6 operational keys (SALES_RECEIVABLE, SALES_REVENUE, CUSTOMER_PAYMENT_RECEIVABLE, PURCHASE_INVENTORY, PURCHASE_PAYABLE, SUPPLIER_PAYMENT_PAYABLE) to GL accounts |
| Cash/Bank GL mapping | ✅ Working | Links CashBankAccount → GL account (Cash/Bank role) |
| Manual Journals | ✅ Working | Draft → Post; balanced validation; business partner + financial class per line |
| General Ledger | ✅ Working | Filterable (account, partner, class, module, date, reference); running balance per account |
| Pending Posting Queue | ✅ Working | Operational transactions (sales, purchases, payments) wait here until posted via profile |
| Journal export | ✅ Working | XLSX + PDF export via `FinancialReportExportService` |

### Accounting (Reporting)
| Feature | Status | Explanation |
|---------|--------|-------------|
| Trial Balance | ✅ Working | Opening/Period/Closing debit/credit; hierarchical totals; zero-balance toggle |
| Profit & Loss (Income Statement) | ✅ Working | Revenue → COGS → Gross Profit → OpEx → Operating Profit → Other Income/Expense → Net Profit; hierarchical sections |
| Balance Sheet | ✅ Working | Assets / Liabilities / Equity; Current Year Earnings (derived from P&L or explicit account); equation balance check |
| Export (XLSX/PDF) | ✅ Working | Custom writers (no external libs); auto-filter in XLSX |

### Dashboard
| Feature | Status | Explanation |
|---------|--------|-------------|
| Sales KPIs | ✅ Working | Posted total, outstanding receivables, recent invoices, 6-month trend |
| Purchase KPIs | ✅ Working | Outstanding payables, recent bills |
| Inventory KPIs | ✅ Working | Warehouses, stocked products, movements, purpose breakdown |
| Financial Position | ✅ Working | Live P&L + Balance Sheet (current year-to-date) |
| Recent payments | ✅ Working | Last 5 posted customer payments |

### Document Attachments
| Feature | Status | Explanation |
|---------|--------|-------------|
| Upload/Download/Delete | ✅ Working | Local filesystem storage (`Attachments__StoragePath`); max 12MB; linked to any aggregate via `SourceReference` |

---

## 3. ACCOUNTING INTEGRATION — VERIFICATION

> **Methodology:** Traced `PostCoreAsync` in each operational service → `AccountingTransaction` creation → `PostOperationalTransactionIfConfiguredAsync` → `EfAccountingService.PostAccountingTransactionAsync` → `BuildOperationalPostingAsync` → JournalEntry + GeneralLedgerEntries.

| Source Document | Creates AccountingTransaction? | Posts to GL via Posting Profile? | Journal Lines Created |
|-----------------|-------------------------------|----------------------------------|----------------------|
| **Sales Invoice (Posted)** | ✅ Yes (`SalesInvoicePosted`) | ✅ Yes | DR Receivable (partner) / CR Revenue |
| **Purchase Invoice (Posted)** | ✅ Yes (`PurchaseInvoicePosted`) | ✅ Yes | DR Inventory / CR Payable (partner) |
| **Customer Payment (Posted)** | ✅ Yes (`CustomerPaymentPosted`) | ✅ Yes | DR Cash/Bank (mapped) / CR Receivable (partner) |
| **Supplier Payment (Posted)** | ✅ Yes (`SupplierPaymentPosted`) | ✅ Yes | DR Payable (partner) / CR Cash/Bank (mapped) |
| **Inventory Receipt (Purchase)** | ✅ Via PurchaseInvoice | ✅ Via PurchaseInvoice | Included in PurchaseInvoice posting |
| **Inventory Issue (Sales)** | ✅ Via SalesInvoice | ✅ Via SalesInvoice | Included in SalesInvoice posting |
| **Production Completion** | ❌ No AccountingTransaction | ❌ No | ProductionConsumption/ProductionReceipt create inventory movements only |
| **Manual Journal** | ✅ Yes (`ManualJournal`) | ✅ Direct (bypasses profile) | User-defined lines |

### Sub-ledgers & Reporting

| Report / Feature | Connected to GL? | Notes |
|------------------|------------------|-------|
| General Ledger | ✅ Yes | Joins `GeneralLedgerEntries` → `JournalEntries` → `AccountingTransactions` |
| Trial Balance | ✅ Yes | Aggregates GL movements by account hierarchy |
| Profit & Loss | ✅ Yes | Derives from GL; Revenue/COGS/Expense by AccountRole |
| Balance Sheet | ✅ Yes | Derives from GL; Current Year Earnings = P&L Net Profit (unless explicit account) |
| Customer Sub-ledger | ✅ Yes | `OpenItems` (Receivable) → SalesInvoices → JournalEntries (via SourceReference) |
| Supplier Sub-ledger | ✅ Yes | `OpenItems` (Payable) → PurchaseInvoices → JournalEntries |
| COGS | ✅ Yes | Mapped via `PURCHASE_INVENTORY` posting key → Inventory account; COGS section in P&L filters `AccountRole == CostOfGoodsSold` |

**Critical gap:** Manufacturing completion (ProductionOrder → Completed) creates inventory movements but **does not create an AccountingTransaction**. No WIP → Finished Goods transfer, no variance posting, no COGS relief at production time.

---

## 4. AUTHENTICATION DETAIL

### Production-Ready
- **JWT Access Tokens** — HS256, configurable expiry, claims: `sub`, `name`, `email`, `role`, `permission`, `company_id`, `company_permission`
- **Google OAuth 2.0** — ID token validation (audience = configured ClientId, issuer = accounts.google.com, email_verified = true)
- **ExternalWorkspace Auto-Provisioning** — On first Google sign-in: creates Organization + Company + Chart of Accounts (6 accounts) + AccountingPeriod (current year) + CashBankAccount (Bank) + PostingProfile (all 6 keys mapped) + Owner role with all `WorkspaceOwner` permissions
- **Company Context** — Frontend `CompanyContextService` + backend `CompanyContextResolver` ensure all requests scoped to single `CompanyId` from JWT
- **Rate Limiting** — Login endpoints use `ProductionSecurity.LoginPolicy`
- **Security Headers** — `UseSecurityHeaders()` middleware in production
- **CORS** — Configured via `Cors__AllowedOrigins__0` (required in production)

### Development-Only
- **DevelopmentIdentitySeeder** — Runs `if (app.Environment.IsDevelopment())` in `Program.cs`
- Requires `DevelopmentAdmin__Email` + `DevelopmentAdmin__Password`
- Creates `DEV` org/company, `Administrator` role with **all** permissions, user with hashed password
- **Never runs in Production** (guarded by environment check)

### Missing / Incomplete
| Feature | Status |
|---------|--------|
| Password reset (email) | ❌ Not implemented |
| User invitation flow | ❌ Not implemented |
| MFA / 2FA | ❌ Not implemented |
| Session revocation (admin) | ❌ Not implemented |
| Audit log of auth events | ❌ Not implemented |
| Google account linking (existing local user) | 🟡 Partial — returns 409 conflict, no link endpoint |

---

## 5. TESTING

### Backend Tests (All Passing ✅)

| Project | Tests | Coverage Focus |
|---------|-------|----------------|
| `Erp.Domain.Tests` | 17 | Domain invariants: account hierarchy, journal balancing, payment allocation, inventory negative stock prevention, invoice immutability after posting, role/user assignment scoping |
| `Erp.Infrastructure.Tests` | 7 | Persistence model: entity mappings, owned types, indexes, sequences |
| `Erp.Api.IntegrationTests` | 41 | Full HTTP pipeline: auth, sales (draft/post/stock/GL), purchasing, payments, inventory, manufacturing, accounting (journals, GL, reports, periods, profiles), admin, master data, company isolation |

**Integration test highlights:**
- `SalesInvoiceEndpointsTests.Posted_invoice_with_an_active_profile_creates_a_balanced_sales_journal_and_ledger_lines` — **verifies end-to-end accounting integration**
- `FinancialReportsEndpointsTests.Reports_use_only_posted_ledger_entries_and_export_valid_documents` — verifies report accuracy + XLSX/PDF export
- `CompanyIsolationEndpointsTests` — verifies tenants cannot see each other's data

### Frontend Tests
- **Cannot run** — Angular 20 requires Node ≥ 20.19 / 22.12; current environment has Node 18.20.5
- **Test files exist:** `*.spec.ts` for components (auth, accounting, dashboard, display-format, master-data, payments, reports, sales) — ~15 spec files

### Obvious Missing Coverage
1. **No E2E tests** (Cypress/Playwright)
2. **No API contract tests** (schema validation)
3. **No performance/load tests**
4. **Frontend unit tests unrunnable** (Node version)
5. **No manufacturing accounting integration tests** (known gap)

---

## 6. CURRENT LIMITATIONS / BUGS (Prioritized)

| # | Area | Issue | Impact |
|---|------|-------|--------|
| 1 | **Manufacturing → Accounting** | Production completion creates no AccountingTransaction → no WIP/COGS/Variance entries | Financial statements incorrect for manufacturers |
| 2 | **Inventory Costing** | No cost method (FIFO/Average/Standard); `costAmount` captured but not used in valuation | COGS & inventory valuation inaccurate |
| 3 | **Password Reset** | No forgot-password, no email infrastructure | Users locked out = admin manual DB fix |
| 4 | **Google Account Linking** | 409 conflict returned but no "link Google to existing account" endpoint | Existing users cannot adopt Google sign-in |
| 5 | **Multi-Currency** | Single base currency per company; no FX rates, no revaluation | Cannot handle foreign currency transactions |
| 6 | **Purchase Invoice Tax** | Tax codes exist in master data but not applied on purchase lines | Tax compliance incomplete |
| 7 | **Sales Credit Notes** | No credit note / return workflow | Cannot handle returns/refunds |
| 8 | **Purchase Credit Notes** | No debit note / return workflow | Cannot handle supplier returns |
| 9 | **Bank Reconciliation** | No reconciliation UI or matching logic | Manual reconciliation only |
| 10 | **Fixed Assets** | No asset register, depreciation, disposal | Missing major accounting module |
| 11 | **Budgeting** | No budget vs actual comparison | Planning gap |
| 12 | **Frontend Node Version** | Requires Node 20+; CI/CD may fail on older runners | Build pipeline risk |

---

## 7. DEPLOYMENT STATUS

### Docker
| File | Purpose | Status |
|------|---------|--------|
| `docker-compose.yml` | Development stack (PostgreSQL + API + Frontend) | ✅ Complete |
| `docker-compose.prod.yml` | Production stack (PostgreSQL + API + Frontend + Nginx + SSL) | ✅ Complete |
| `docker-compose.override.yml` | Local overrides | ✅ Present |
| `backend/Dockerfile` | Multi-stage (build → runtime); non-root user | ✅ Complete |
| `frontend/Dockerfile` | Nginx + Angular build; healthcheck | ✅ Complete |
| `nginx.prod.conf` | Reverse proxy, SSL termination, rate limiting | ✅ Referenced in prod compose |

### Environment Variables (Required for Production)

| Variable | Required? | Notes |
|----------|-----------|-------|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | ✅ | Database credentials |
| `JWT_ISSUER` / `JWT_AUDIENCE` / `JWT_SIGNING_KEY` | ✅ | **SigningKey ≥ 32 chars** |
| `CORS_ALLOWED_ORIGIN` | ✅ | Single origin (e.g., `https://erp.example.com`) |
| `ALLOWED_HOSTS` | ✅ | Comma-separated hostnames |
| `GOOGLE_AUTHENTICATION_ENABLED` | Optional | `true`/`false` |
| `GOOGLE_CLIENT_ID` | If Google enabled | OAuth client ID |
| `EXTERNAL_WORKSPACE_BASE_CURRENCY` | Optional | Default `USD` (3-letter ISO) |
| `REVERSE_PROXY_IP` | Optional | For forwarded headers (default `172.20.0.1`) |

### Supabase / PostgreSQL
- **Standard PostgreSQL 16** — no Supabase-specific config
- **EF Core migrations** — run via `dotnet-ef` (local tool pinned)
- **Connection string** — `Host=postgres;Port=5432;Database=...` (internal Docker network)

### Render / Cloud Deployment
- **No Render-specific config** (no `render.yaml`, no `Dockerfile.render`)
- **Deployable to any container platform** (Render, Fly.io, Railway, AWS ECS, Azure Container Apps, GCP Cloud Run)
- **Health endpoints:** `/health` (liveness), `/health/ready` (readiness + DB connectivity)

---

## 8. PART 2 VIDEO OPPORTUNITIES

*Ranked by visual impact, AI coding demonstration value, ERP usefulness, and implementation feasibility.*

| # | Feature | Why It's Good for Video | Touches |
|---|---------|------------------------|---------|
| 1 | **Manufacturing Cost Accounting** | **Highest impact** — connects Manufacturing → Inventory → Accounting; demonstrates domain-driven design, transactional consistency, GL posting | Manufacturing, Inventory, Accounting (PostingProfile, JournalEntry, GL) |
| 2 | **Password Reset Flow (Email + Token)** | Real-world auth feature; shows frontend-backend coordination, security considerations, email abstraction | Auth, Email service, Frontend forms, JWT token design |
| 3 | **Sales Credit Notes / Returns** | Complete the sales cycle; shows correcting entries, inventory reversal, receivable adjustment, GL impact | Sales, Inventory, Accounting, OpenItems |
| 4 | **Bank Reconciliation UI** | Visually satisfying (matching UI); demonstrates complex state management, filtering, bulk operations | Payments, CashBankAccounts, GL, Frontend (drag-drop/checkbox grid) |
| 5 | **Multi-Currency Foundation** | Core ERP capability; shows exchange rate service, revaluation journal, realized/unrealized gains | Master data (Currency, FXRate), Accounting (revaluation), Reporting |
| 6 | **Purchase Credit Notes / Supplier Returns** | Mirrors sales returns; good symmetry demonstration | Purchasing, Inventory, Accounting, Payables |
| 7 | **Budget vs Actual Dashboard Widget** | Visual charts (already have P&L data); shows financial reporting extension, frontend charting | Accounting, Reports, Frontend (Chart.js/Recharts) |
| 8 | **Google Account Linking for Existing Users** | Real OAuth edge case; shows account merging, security flow | Auth, ExternalIdentity, Frontend UX |

**Recommended Part 2 sequence:** 1 → 2 → 3 (each builds on the last; 1 is the "big feature", 2 is "auth hardening", 3 completes sales cycle)

---

## 9. FINAL SUMMARY

### A. What Is Already Solid ✅
1. **Clean Architecture** — Domain-driven, testable, EF Core properly isolated
2. **Authentication** — Production-ready JWT + Google OAuth with auto-provisioning
3. **Multi-company Isolation** — Row-level security via `CompanyId` everywhere
4. **Core Accounting Engine** — Chart of Accounts, Posting Profiles, Manual Journals, GL, Trial Balance, P&L, Balance Sheet, Export
5. **Operational → Accounting Integration** — Sales, Purchases, Payments all post to GL automatically when profile configured
6. **Inventory with Concurrency Control** — Serializable transactions + advisory locks prevent negative stock
7. **Comprehensive Integration Tests** — 41 tests covering auth, all modules, company isolation
8. **Docker Production Config** — Nginx reverse proxy, health checks, non-root containers, SSL-ready

### B. What Is Incomplete ❌ / 🟡
| Priority | Area | Gap |
|----------|------|-----|
| **Critical** | Manufacturing Accounting | No GL posting on production completion |
| **Critical** | Inventory Costing | No cost method → inaccurate COGS/valuation |
| **High** | Password Reset | No self-service recovery |
| **High** | Multi-Currency | Single currency only |
| **Medium** | Credit Notes (Sales/Purchase) | No return/refund workflow |
| **Medium** | Bank Reconciliation | No matching UI |
| **Medium** | Fixed Assets / Budgeting | Missing modules |
| **Low** | Google Account Linking | 409 conflict but no resolution |

### C. Top 3–5 Most Interesting Part 2 Builds

| Rank | Feature | Est. Effort | Video Appeal |
|------|---------|-------------|--------------|
| 1 | **Manufacturing Cost Accounting** | Medium | ⭐⭐⭐⭐⭐ — Connects 3 domains; shows DDD + transactional consistency |
| 2 | **Password Reset Flow** | Low–Medium | ⭐⭐⭐⭐ — Auth hardening; frontend+backend+email abstraction |
| 3 | **Sales Credit Notes** | Medium | ⭐⭐⭐⭐ — Completes sales cycle; inventory+GL reversal |
| 4 | **Bank Reconciliation** | Medium | ⭐⭐⭐⭐ — Visual matching UI; complex state |
| 5 | **Multi-Currency Foundation** | High | ⭐⭐⭐ — Foundational; exchange rates + revaluation |

### D. Risks for Part 2

| Risk | Mitigation |
|------|------------|
| **Manufacturing accounting requires schema changes** (WIP accounts, variance accounts, new posting keys) | Plan migration upfront; use `PostingKeys` extension pattern |
| **Inventory costing needs design decision** (FIFO vs Average vs Standard) — affects all valuation | Pick **Weighted Average** for simplicity; add `CostMethod` on Product |
| **Email infrastructure** for password reset — no SMTP config, no template system | Abstract `IEmailService`; use dev console logger; swap for SendGrid/Postmark later |
| **Frontend Node version mismatch** (Angular 20 needs Node 20+) | Update CI/CD; document in `README.md`; use `nvm`/`fnm` locally |
| **Google account linking UX** — needs "link account" page + merge logic | Build as separate flow; don't rush — can defer to Part 3 |

---

**Bottom line:** The RABT ERP is a **genuinely functional, well-architected foundation** with working double-entry accounting, operational→GL integration, multi-tenant isolation, and production-grade auth. The most impressive gap — and the best Part 2 demo — is **closing the Manufacturing → Accounting loop**, which touches the most domains and showcases AI-assisted domain modeling.

*Report generated by automated codebase audit — no code was modified.*