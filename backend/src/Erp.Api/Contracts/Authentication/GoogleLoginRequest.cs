using System.ComponentModel.DataAnnotations;

namespace Erp.Api.Contracts.Authentication;

public sealed class GoogleLoginRequest
{
    [Required]
    [StringLength(16384, MinimumLength = 20)]
    public string IdToken { get; init; } = string.Empty;
}
