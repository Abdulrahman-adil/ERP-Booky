using Erp.Domain.Common;

namespace Erp.Domain.Identity;

#pragma warning disable CS8618

public sealed class Permission : Entity
{
    private Permission()
    {
    }

    public Permission(string key, string name, string? description = null, Guid id = default)
        : base(id)
    {
        Key = Money.RequireText(key, nameof(key));
        Name = Money.RequireText(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
    }

    public string Key { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }
}

public sealed class Role : AggregateRoot
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    public Role(Guid companyId, string name, string? description = null, Guid id = default)
        : base(id)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        CompanyId = companyId;
        Name = Money.RequireText(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public void Update(string name, string? description)
    {
        Name = Money.RequireText(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void GrantPermission(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("A permission is required.", nameof(permissionId));
        }

        if (_permissions.Any(permission => permission.PermissionId == permissionId))
        {
            return;
        }

        _permissions.Add(new RolePermission(Id, permissionId));
    }

    public void ReplacePermissions(IEnumerable<Guid> permissionIds)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);

        var distinctPermissionIds = permissionIds.Distinct().ToArray();
        if (distinctPermissionIds.Any(permissionId => permissionId == Guid.Empty))
        {
            throw new ArgumentException("A permission is required.", nameof(permissionIds));
        }

        _permissions.Clear();
        foreach (var permissionId in distinctPermissionIds)
        {
            _permissions.Add(new RolePermission(Id, permissionId));
        }
    }
}

public sealed class RolePermission : Entity
{
    private RolePermission()
    {
    }

    internal RolePermission(Guid roleId, Guid permissionId, Guid id = default)
        : base(id)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }
}

public sealed class User : AggregateRoot
{
    private readonly List<UserRoleAssignment> _roleAssignments = [];

    private User()
    {
    }

    public User(string displayName, string email, Guid id = default)
        : base(id)
    {
        DisplayName = Money.RequireText(displayName, nameof(displayName));
        Email = NormalizeEmail(email);
        IsActive = true;
    }

    public string DisplayName { get; private set; }

    public string Email { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    public void Update(string displayName, string email)
    {
        DisplayName = Money.RequireText(displayName, nameof(displayName));
        Email = NormalizeEmail(email);
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void AssignRole(Guid companyId, Guid roleId)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("A role is required.", nameof(roleId));
        }

        if (_roleAssignments.Any(assignment => assignment.CompanyId == companyId && assignment.RoleId == roleId))
        {
            return;
        }

        _roleAssignments.Add(new UserRoleAssignment(Id, companyId, roleId));
    }

    public void ReplaceRoles(Guid companyId, IEnumerable<Guid> roleIds)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company is required.", nameof(companyId));
        }

        ArgumentNullException.ThrowIfNull(roleIds);
        var distinctRoleIds = roleIds.Distinct().ToArray();
        if (distinctRoleIds.Any(roleId => roleId == Guid.Empty))
        {
            throw new ArgumentException("A role is required.", nameof(roleIds));
        }

        _roleAssignments.RemoveAll(assignment => assignment.CompanyId == companyId);
        foreach (var roleId in distinctRoleIds)
        {
            _roleAssignments.Add(new UserRoleAssignment(Id, companyId, roleId));
        }
    }

    private static string NormalizeEmail(string email)
    {
        var normalizedEmail = Money.RequireText(email, nameof(email)).ToLowerInvariant();

        if (!normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("A valid email address is required.", nameof(email));
        }

        return normalizedEmail;
    }
}

public sealed class UserRoleAssignment : Entity
{
    private UserRoleAssignment()
    {
    }

    internal UserRoleAssignment(Guid userId, Guid companyId, Guid roleId, Guid id = default)
        : base(id)
    {
        UserId = userId;
        CompanyId = companyId;
        RoleId = roleId;
    }

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid RoleId { get; private set; }
}

#pragma warning restore CS8618
