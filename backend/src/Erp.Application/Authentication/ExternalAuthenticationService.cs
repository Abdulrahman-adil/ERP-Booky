namespace Erp.Application.Authentication;

public sealed class ExternalAuthenticationService(
    IGoogleIdTokenValidator googleIdTokenValidator,
    IExternalWorkspaceProvisioner externalWorkspaceProvisioner,
    IAccessTokenService accessTokenService) : IExternalAuthenticationService
{
    public async Task<ExternalAuthenticationResult> LoginWithGoogleAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return new ExternalAuthenticationResult(ExternalAuthenticationStatus.InvalidCredential, null);
        }

        var identity = await googleIdTokenValidator.ValidateAsync(idToken, cancellationToken);

        if (identity is null)
        {
            return new ExternalAuthenticationResult(ExternalAuthenticationStatus.InvalidCredential, null);
        }

        var provisioningResult = await externalWorkspaceProvisioner.FindOrProvisionGoogleWorkspaceAsync(identity, cancellationToken);

        return provisioningResult.Status switch
        {
            ExternalWorkspaceProvisioningStatus.Authenticated when provisioningResult.User is not null =>
                new ExternalAuthenticationResult(
                    ExternalAuthenticationStatus.Succeeded,
                    new AuthenticationResult(accessTokenService.Create(provisioningResult.User), provisioningResult.User)),
            ExternalWorkspaceProvisioningStatus.ExistingAccountRequiresLink =>
                new ExternalAuthenticationResult(ExternalAuthenticationStatus.ExistingAccountRequiresLink, null),
            ExternalWorkspaceProvisioningStatus.AccountInactive =>
                new ExternalAuthenticationResult(ExternalAuthenticationStatus.AccountInactive, null),
            _ => new ExternalAuthenticationResult(ExternalAuthenticationStatus.InvalidCredential, null)
        };
    }
}
