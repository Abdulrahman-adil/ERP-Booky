using Erp.Domain.Common;

namespace Erp.Domain.Identity;

#pragma warning disable CS8618

public enum ExternalIdentityProvider
{
    Google
}

public sealed class ExternalIdentity : Entity
{
    private ExternalIdentity()
    {
    }

    public ExternalIdentity(
        Guid userId,
        ExternalIdentityProvider provider,
        string providerSubject,
        Guid id = default)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        UserId = userId;
        Provider = provider;
        ProviderSubject = Money.RequireText(providerSubject, nameof(providerSubject));
        LinkedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }

    public ExternalIdentityProvider Provider { get; private set; }

    public string ProviderSubject { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }
}

#pragma warning restore CS8618
