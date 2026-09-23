using System.Globalization;
using Erp.Application.Authentication;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Erp.Api.Authentication;

public sealed class GoogleIdTokenValidator(IOptions<GoogleAuthenticationOptions> options) : IGoogleIdTokenValidator
{
    private static readonly string[] ValidIssuers = ["accounts.google.com", "https://accounts.google.com"];

    public async Task<VerifiedGoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [settings.ClientId.Trim()]
                });

            if (!ValidIssuers.Contains(payload.Issuer, StringComparer.Ordinal)
                || string.IsNullOrWhiteSpace(payload.Subject)
                || string.IsNullOrWhiteSpace(payload.Email)
                || payload.Subject.Length > 255
                || payload.Email.Length > 254
                || !Convert.ToBoolean(payload.EmailVerified, CultureInfo.InvariantCulture))
            {
                return null;
            }

            var displayName = string.IsNullOrWhiteSpace(payload.Name)
                ? payload.Email.Split('@', 2)[0]
                : payload.Name.Trim();

            return new VerifiedGoogleIdentity(payload.Subject.Trim(), payload.Email.Trim(), displayName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
