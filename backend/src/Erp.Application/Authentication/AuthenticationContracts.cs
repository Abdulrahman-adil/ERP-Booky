namespace Erp.Application.Authentication;

public sealed record AuthenticatedUser(
    Guid Id,
    string DisplayName,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<Guid> CompanyIds,
    IReadOnlyCollection<CompanyPermission> CompanyPermissions,
    DateTimeOffset? PasswordChangedAt = null);

public sealed record CompanyPermission(Guid CompanyId, string Permission);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record AuthenticationResult(AccessToken AccessToken, AuthenticatedUser User);

public interface IAuthenticationService
{
    Task<AuthenticationResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUserAuthenticationStore
{
    Task<UserAuthenticationRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IPasswordHashingService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

public interface IAccessTokenService
{
    AccessToken Create(AuthenticatedUser user);
}

public sealed record UserAuthenticationRecord(AuthenticatedUser User, string PasswordHash);

public sealed record VerifiedGoogleIdentity(string Subject, string Email, string DisplayName);

public interface IGoogleIdTokenValidator
{
    Task<VerifiedGoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public enum ExternalWorkspaceProvisioningStatus
{
    Authenticated,
    ExistingAccountRequiresLink,
    AccountInactive
}

public sealed record ExternalWorkspaceProvisioningResult(
    ExternalWorkspaceProvisioningStatus Status,
    AuthenticatedUser? User,
    bool WorkspaceCreated);

public interface IExternalWorkspaceProvisioner
{
    Task<ExternalWorkspaceProvisioningResult> FindOrProvisionGoogleWorkspaceAsync(
        VerifiedGoogleIdentity identity,
        CancellationToken cancellationToken = default);
}

public enum ExternalAuthenticationStatus
{
    Succeeded,
    InvalidCredential,
    ExistingAccountRequiresLink,
    AccountInactive
}

public sealed record ExternalAuthenticationResult(
    ExternalAuthenticationStatus Status,
    AuthenticationResult? Authentication);

public interface IExternalAuthenticationService
{
    Task<ExternalAuthenticationResult> LoginWithGoogleAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
