namespace Erp.Api.Authentication;

public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "Authentication:Google";

    public bool Enabled { get; init; }

    public string ClientId { get; init; } = string.Empty;
}
