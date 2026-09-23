namespace Erp.Infrastructure.Authentication;

public sealed class ExternalWorkspaceOptions
{
    public const string SectionName = "Authentication:ExternalWorkspace";

    public string BaseCurrencyCode { get; init; } = "USD";
}
