using Erp.Application.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Erp.Infrastructure.Authentication;

public sealed class PasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("A password is required.", nameof(password));
        }

        return _passwordHasher.HashPassword(new object(), password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(
            new object(),
            passwordHash,
            password);

        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
