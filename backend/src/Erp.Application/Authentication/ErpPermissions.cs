namespace Erp.Application.Authentication;

public static class ErpPermissions
{
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string ManufacturingView = "manufacturing.view";
    public const string ManufacturingManage = "manufacturing.manage";
    public const string SalesView = "sales.view";
    public const string SalesManage = "sales.manage";
    public const string PaymentsView = "payments.view";
    public const string PaymentsManage = "payments.manage";
    public const string ReceivablesView = "receivables.view";
    public const string CashBankManage = "cash-bank.manage";
    public const string PurchasesView = "purchases.view";
    public const string PurchasesManage = "purchases.manage";
    public const string PayablesView = "payables.view";
    public const string PayablesManage = "payables.manage";
    public const string SupplierPaymentsView = "supplierpayments.view";
    public const string SupplierPaymentsManage = "supplierpayments.manage";
    public const string AccountingView = "accounting.view";
    public const string AccountingManage = "accounting.manage";
    public const string JournalsView = "journals.view";
    public const string JournalsManage = "journals.manage";
    public const string JournalsPost = "journals.post";
    public const string ChartOfAccountsManage = "chartofaccounts.manage";
    public const string AccountingConfigurationManage = "accountingconfiguration.manage";
    public const string DocumentsView = "documents.view";
    public const string DocumentsManage = "documents.manage";
    public const string ReportsView = "reports.view";
    public const string FinancialReportsView = "financialreports.view";
    public const string FinancialReportsExport = "financialreports.export";
    public const string UsersManage = "users.manage";
    public const string DevelopmentReset = "development.reset";

    public static IReadOnlyCollection<string> All { get; } =
    [
        CustomersView,
        CustomersManage,
        ProductsView,
        ProductsManage,
        InventoryView,
        InventoryManage,
        ManufacturingView,
        ManufacturingManage,
        SalesView,
        SalesManage,
        PaymentsView,
        PaymentsManage,
        ReceivablesView,
        CashBankManage,
        PurchasesView,
        PurchasesManage,
        PayablesView,
        PayablesManage,
        SupplierPaymentsView,
        SupplierPaymentsManage,
        AccountingView,
        AccountingManage,
        JournalsView,
        JournalsManage,
        JournalsPost,
        ChartOfAccountsManage,
        AccountingConfigurationManage,
        DocumentsView,
        DocumentsManage,
        ReportsView,
        FinancialReportsView,
        FinancialReportsExport,
        UsersManage,
        DevelopmentReset
    ];

    public static IReadOnlyCollection<string> WorkspaceOwner { get; } =
        All.Where(permission => !string.Equals(permission, DevelopmentReset, StringComparison.Ordinal)).ToArray();
}

public static class AuthorizationClaimTypes
{
    public const string Permission = "permission";

    public const string CompanyId = "company_id";

    public const string CompanyPermission = "company_permission";

    public static string FormatCompanyPermission(Guid companyId, string permission) =>
        $"{companyId:D}|{permission}";
}

public static class PermissionPolicies
{
    public static string For(string permission) => $"permission:{permission}";
}
