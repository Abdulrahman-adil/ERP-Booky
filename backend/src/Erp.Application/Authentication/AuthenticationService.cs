namespace Erp.Application.Authentication;

public sealed class AuthenticationService(
    IUserAuthenticationStore userAuthenticationStore,
    IPasswordHashingService passwordHashingService,
    IAccessTokenService accessTokenService) : IAuthenticationService
{
    public async Task<AuthenticationResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var userRecord = await userAuthenticationStore.FindByEmailAsync(
            email.Trim().ToLowerInvariant(),
            cancellationToken);

        if (userRecord is null || !passwordHashingService.Verify(userRecord.PasswordHash, password))
        {
            return null;
        }

        return new AuthenticationResult(accessTokenService.Create(userRecord.User), userRecord.User);
    }

    public Task<AuthenticatedUser?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return userId == Guid.Empty
            ? Task.FromResult<AuthenticatedUser?>(null)
            : userAuthenticationStore.FindActiveUserAsync(userId, cancellationToken);
    }
}
