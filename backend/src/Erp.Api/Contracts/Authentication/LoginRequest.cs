using System.ComponentModel.DataAnnotations;

namespace Erp.Api.Contracts.Authentication;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
