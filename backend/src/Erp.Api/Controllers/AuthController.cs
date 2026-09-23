using System.Security.Claims;
using Erp.Api.Authentication;
using Erp.Api.Contracts.Authentication;
using Erp.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using Erp.Api.Security;

namespace Erp.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Authentication")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IExternalAuthenticationService externalAuthenticationService,
    IOptions<GoogleAuthenticationOptions> googleAuthenticationOptions) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(ProductionSecurity.LoginPolicy)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var authenticationResult = await authenticationService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (authenticationResult is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid email or password.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(LoginResponse.From(authenticationResult));
    }

    [AllowAnonymous]
    [HttpPost("google")]
    [EnableRateLimiting(ProductionSecurity.LoginPolicy)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> LoginWithGoogle(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var settings = googleAuthenticationOptions.Value;

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ClientId))
        {
            return NotFound();
        }

        var authenticationResult = await externalAuthenticationService.LoginWithGoogleAsync(request.IdToken, cancellationToken);

        return authenticationResult.Status switch
        {
            ExternalAuthenticationStatus.Succeeded when authenticationResult.Authentication is not null => Ok(LoginResponse.From(authenticationResult.Authentication)),
            ExternalAuthenticationStatus.ExistingAccountRequiresLink => Conflict(CreateGoogleAccountLinkRequiredProblem()),
            ExternalAuthenticationStatus.AccountInactive => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "This account is inactive.",
                Instance = HttpContext.Request.Path
            }),
            _ => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "The Google identity token is invalid.",
                Instance = HttpContext.Request.Path
            })
        };
    }

    private ProblemDetails CreateGoogleAccountLinkRequiredProblem()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "This email belongs to an existing ERP account. Sign in with its password before linking Google.",
            Type = "urn:rabt-erp:problem:google-account-link-required",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "google_account_link_required";
        return problem;
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await authenticationService.GetCurrentUserAsync(userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role)
            .ToArray();
        var permissions = User.FindAll(AuthorizationClaimTypes.Permission)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(permission => permission)
            .ToArray();
        var companyIds = User.FindAll(AuthorizationClaimTypes.CompanyId)
            .Select(claim => Guid.TryParse(claim.Value, out var companyId) ? companyId : Guid.Empty)
            .Where(companyId => companyId != Guid.Empty)
            .Distinct()
            .OrderBy(companyId => companyId)
            .ToArray();

        return Ok(new CurrentUserResponse(
            user.Id,
            user.DisplayName,
            user.Email,
            roles,
            permissions,
            companyIds));
    }
}
