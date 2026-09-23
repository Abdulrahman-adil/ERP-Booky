using Erp.Application.Authentication;

namespace Erp.Api.Contracts.Authentication;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserResponse User)
{
    public static LoginResponse From(AuthenticationResult result) => new(
        result.AccessToken.Value,
        result.AccessToken.ExpiresAt,
        CurrentUserResponse.From(result.User));
}

public sealed record CurrentUserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<Guid> CompanyIds)
{
    public static CurrentUserResponse From(AuthenticatedUser user) => new(
        user.Id,
        user.DisplayName,
        user.Email,
        user.Roles,
        user.Permissions,
        user.CompanyIds);
}
