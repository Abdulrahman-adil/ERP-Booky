namespace Erp.Infrastructure.Authentication;

public sealed class UserCredential
{
    private UserCredential()
    {
        PasswordHash = null!;
    }

    public UserCredential(Guid userId, string passwordHash)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        UserId = userId;
        Id = Guid.NewGuid();
        PasswordHash = null!;
        SetPasswordHash(passwordHash);
        PasswordChangedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset PasswordChangedAt { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        PasswordChangedAt = DateTimeOffset.UtcNow;
    }
}
