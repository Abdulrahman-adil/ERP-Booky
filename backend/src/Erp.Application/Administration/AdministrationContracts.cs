namespace Erp.Application.Administration;

public sealed record AdministrationPermissionDto(Guid Id, string Key, string Name, string? Description, bool IsActive);

public sealed record AdministrationRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<Guid> PermissionIds,
    int AssignedUserCount);

public sealed record AdministrationUserDto(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsActive,
    IReadOnlyCollection<Guid> RoleIds,
    IReadOnlyCollection<string> RoleNames,
    DateTimeOffset? PasswordChangedAt);

public sealed record AdministrationUserInput(
    string DisplayName,
    string Email,
    IReadOnlyCollection<Guid> RoleIds,
    string? Password = null);

public sealed record AdministrationRoleInput(
    string Name,
    string? Description,
    IReadOnlyCollection<Guid> PermissionIds);

public sealed record DevelopmentResetPreview(
    int SalesInvoices,
    int PurchaseBills,
    int CustomerPayments,
    int SupplierPayments,
    int InventoryMovements,
    int OpenItems,
    int JournalEntries,
    int GeneralLedgerEntries,
    int PendingAccountingTransactions,
    int Attachments,
    int Customers,
    int Suppliers,
    int Products,
    int Warehouses);

public sealed record DevelopmentResetResult(DevelopmentResetPreview Cleared, DateTimeOffset CompletedAt);

public interface IAdministrationService
{
    Task<IReadOnlyCollection<AdministrationPermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AdministrationRoleDto>> GetRolesAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<AdministrationRoleDto> CreateRoleAsync(Guid companyId, AdministrationRoleInput input, CancellationToken cancellationToken = default);
    Task<AdministrationRoleDto?> UpdateRoleAsync(Guid companyId, Guid id, AdministrationRoleInput input, CancellationToken cancellationToken = default);
    Task<bool> SetRoleActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AdministrationUserDto>> GetUsersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<AdministrationUserDto> CreateUserAsync(Guid companyId, AdministrationUserInput input, CancellationToken cancellationToken = default);
    Task<AdministrationUserDto?> UpdateUserAsync(Guid companyId, Guid id, AdministrationUserInput input, CancellationToken cancellationToken = default);
    Task<bool> SetUserActiveAsync(Guid companyId, Guid actorUserId, Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public interface IDevelopmentDataResetService
{
    Task<DevelopmentResetPreview> GetPreviewAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<DevelopmentResetResult> ResetAsync(Guid companyId, string confirmation, CancellationToken cancellationToken = default);
}

public sealed class AdministrationValidationException(string message) : Exception(message);

public sealed class AdministrationConflictException(string message) : Exception(message);
