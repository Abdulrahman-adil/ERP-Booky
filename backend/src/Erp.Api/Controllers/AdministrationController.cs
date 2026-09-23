using System.Security.Claims;
using Erp.Api.Contracts.Administration;
using Erp.Application.Administration;
using Erp.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/administration"), Tags("Administration")]
public sealed class AdministrationController(
    IAdministrationService administrationService,
    IDevelopmentDataResetService developmentDataResetService,
    IWebHostEnvironment environment) : CompanyScopedControllerBase
{
    [HttpGet("permissions"), Authorize(Policy = "permission:users.manage")]
    public Task<IReadOnlyCollection<AdministrationPermissionDto>> GetPermissions(CancellationToken cancellationToken) =>
        administrationService.GetPermissionsAsync(cancellationToken);

    [HttpGet("roles"), Authorize(Policy = "permission:users.manage")]
    public async Task<ActionResult<IReadOnlyCollection<AdministrationRoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await administrationService.GetRolesAsync(companyId, cancellationToken));
    }

    [HttpPost("roles"), Authorize(Policy = "permission:users.manage")]
    public async Task<ActionResult<AdministrationRoleDto>> CreateRole(AdministrationRoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var role = await administrationService.CreateRoleAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(GetRoles), role);
    }

    [HttpPut("roles/{id:guid}"), Authorize(Policy = "permission:users.manage")]
    public async Task<ActionResult<AdministrationRoleDto>> UpdateRole(Guid id, AdministrationRoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var role = await administrationService.UpdateRoleAsync(companyId, id, request.ToInput(), cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPut("roles/{id:guid}/status"), Authorize(Policy = "permission:users.manage")]
    public async Task<IActionResult> SetRoleStatus(Guid id, AdministrationStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return await administrationService.SetRoleActiveAsync(companyId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpGet("users"), Authorize(Policy = "permission:users.manage")]
    public async Task<ActionResult<IReadOnlyCollection<AdministrationUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await administrationService.GetUsersAsync(companyId, cancellationToken));
    }

    [HttpPost("users"), Authorize(Policy = "permission:users.manage")]
    public async Task<ActionResult<AdministrationUserDto>> CreateUser(AdministrationUserRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var user = await administrationService.CreateUserAsync(companyId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(GetUsers), user);
    }

    [HttpPut("users/{id:guid}"), Authorize(Policy = "permission:users.manage")]
    public async Task<ActionResult<AdministrationUserDto>> UpdateUser(Guid id, AdministrationUserRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId)) return Forbid();
        var user = await administrationService.UpdateUserAsync(companyId, id, request.ToInput(), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("users/{id:guid}/status"), Authorize(Policy = "permission:users.manage")]
    public async Task<IActionResult> SetUserStatus(Guid id, AdministrationStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryCompany(out var companyId) || !TryUser(out var actorUserId)) return Forbid();
        return await administrationService.SetUserActiveAsync(companyId, actorUserId, id, request.IsActive, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpGet("development-reset"), Authorize(Policy = "permission:development.reset")]
    public async Task<ActionResult<DevelopmentResetPreview>> GetDevelopmentResetPreview(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await developmentDataResetService.GetPreviewAsync(companyId, cancellationToken));
    }

    [HttpPost("development-reset"), Authorize(Policy = "permission:development.reset")]
    public async Task<ActionResult<DevelopmentResetResult>> ResetDevelopmentData(DevelopmentResetRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (!TryCompany(out var companyId)) return Forbid();
        return Ok(await developmentDataResetService.ResetAsync(companyId, request.Confirmation, cancellationToken));
    }
}
