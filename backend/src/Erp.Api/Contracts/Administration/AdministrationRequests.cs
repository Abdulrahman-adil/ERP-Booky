using System.ComponentModel.DataAnnotations;
using Erp.Application.Administration;

namespace Erp.Api.Contracts.Administration;

public sealed class AdministrationUserRequest
{
    [Required, MaxLength(200)] public string DisplayName { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(254)] public string Email { get; init; } = string.Empty;
    public IReadOnlyCollection<Guid> RoleIds { get; init; } = [];
    [MinLength(10), MaxLength(200)] public string? Password { get; init; }

    public AdministrationUserInput ToInput() => new(DisplayName, Email, RoleIds, Password);
}

public sealed class AdministrationRoleRequest
{
    [Required, MaxLength(100)] public string Name { get; init; } = string.Empty;
    [MaxLength(500)] public string? Description { get; init; }
    public IReadOnlyCollection<Guid> PermissionIds { get; init; } = [];

    public AdministrationRoleInput ToInput() => new(Name, Description, PermissionIds);
}

public sealed class AdministrationStatusRequest
{
    public bool IsActive { get; init; }
}

public sealed class DevelopmentResetRequest
{
    [Required, MaxLength(100)] public string Confirmation { get; init; } = string.Empty;
}
