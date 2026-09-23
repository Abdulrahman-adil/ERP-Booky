namespace Erp.Infrastructure.Authentication;

public sealed class DevelopmentAdminOptions
{
    public const string SectionName = "DevelopmentAdmin";

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string DisplayName { get; init; } = "Development Administrator";

    public string OrganizationName { get; init; } = "Development Organization";

    public string OrganizationCode { get; init; } = "DEV";

    public string CompanyName { get; init; } = "Development Company";

    public string CompanyCode { get; init; } = "DEV";

    public string BaseCurrencyCode { get; init; } = "USD";
}
