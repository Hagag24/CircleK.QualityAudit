using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Reports.Models;
using CircleK.QualityAudit.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircleK.QualityAudit.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly IAuthService _authService;

    public ReportsController(IReportService reports, IAuthService authService)
    {
        _reports = reports;
        _authService = authService;
    }

    private bool HasPermission(string permission)
        => User.HasClaim("permission", permission);

    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionConstants.ReportView)]
    public async Task<ActionResult<DashboardReportDto>> GetDashboard(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? auditId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(PermissionConstants.AuditViewAll))
        {
            if (!user.BranchId.HasValue)
            {
                return Forbid();
            }

            branchId = user.BranchId.Value;
        }

        var query = new ReportQuery(brandId, from, to, search, branchId, auditId);
        var result = await _reports.GetDashboardAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("branch-scores")]
    [Authorize(Policy = PermissionConstants.ReportView)]
    public async Task<ActionResult<IReadOnlyList<BranchRiskRowDto>>> GetBranchScores(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? auditId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(PermissionConstants.AuditViewAll))
        {
            if (!user.BranchId.HasValue)
            {
                return Forbid();
            }

            branchId = user.BranchId.Value;
        }

        var query = new ReportQuery(brandId, from, to, search, branchId, auditId);
        var result = await _reports.GetBranchScoresAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("critical-items")]
    [Authorize(Policy = PermissionConstants.ReportView)]
    public async Task<ActionResult<IReadOnlyList<CriticalItemDto>>> GetCriticalItems(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? auditId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(PermissionConstants.AuditViewAll))
        {
            if (!user.BranchId.HasValue)
            {
                return Forbid();
            }

            branchId = user.BranchId.Value;
        }

        var query = new ReportQuery(brandId, from, to, search, branchId, auditId);
        var result = await _reports.GetCriticalItemsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("export")]
    [Authorize(Policy = PermissionConstants.ReportExport)]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? auditId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(PermissionConstants.AuditViewAll))
        {
            if (!user.BranchId.HasValue)
            {
                return Forbid();
            }

            branchId = user.BranchId.Value;
        }

        var query = new ReportQuery(brandId, from, to, search, branchId, auditId);
        var result = await _reports.ExportAsync(query, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return File(result.Content, result.ContentType, result.FileName);
    }
}
