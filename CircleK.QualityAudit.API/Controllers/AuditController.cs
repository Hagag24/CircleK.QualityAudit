using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Audits.Models;
using CircleK.QualityAudit.Domain.Enums;
using CircleK.QualityAudit.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace CircleK.QualityAudit.API.Controllers;

[ApiController]
[Route("api/audits")]
[Authorize]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly IAuthService _authService;

    public AuditController(IAuditService auditService, IAuthService authService)
    {
        _auditService = auditService;
        _authService = authService;
    }

    private bool HasPermission(string permission)
        => User.HasClaim("permission", permission);

    [HttpPost]
    [Authorize(Policy = PermissionConstants.AuditCreate)]
    public async Task<ActionResult<AuditSessionDto>> Create([FromBody] CreateAuditRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(PermissionConstants.AuditViewAll))
        {
            if (!user.BranchId.HasValue || user.BranchId.Value != request.BranchId)
            {
                return Forbid();
            }
        }

        if (request.CurrentEmployeeCount.HasValue && request.CurrentEmployeeCount.Value < 0)
        {
            return BadRequest("Employee count must be zero or more.");
        }

        var result = await _auditService.CreateAuditAsync(request, user.Id, cancellationToken);
        return result is null ? BadRequest("Invalid branch or template.") : Ok(result);
    }

    [HttpGet("{id:guid}/form")]
    [Authorize(Policy = "Audit.ViewAny")]
    public async Task<ActionResult<AuditFormDto>> GetForm(Guid id, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var access = await _auditService.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        if (!CanViewSession(access, user))
        {
            return Forbid();
        }

        var result = await _auditService.GetAuditFormAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/answers")]
    [Authorize(Policy = PermissionConstants.AuditEdit)]
    public async Task<ActionResult<AuditAnswerDto>> SaveAnswer(Guid id, [FromBody] SaveAnswerRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var access = await _auditService.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        if (!CanEditSession(access, user))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(AnswerType), request.Answer))
        {
            return BadRequest("Invalid answer type.");
        }

        var result = await _auditService.SaveAnswerAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/timings")]
    [Authorize(Policy = PermissionConstants.AuditEdit)]
    public async Task<ActionResult<AuditItemTimingDto>> AddTiming(Guid id, [FromBody] SaveTimingRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var access = await _auditService.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        if (!CanEditSession(access, user))
        {
            return Forbid();
        }

        if (request.DurationSeconds <= 0)
        {
            return BadRequest("Duration must be positive.");
        }

        var result = await _auditService.AddTimingAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = PermissionConstants.AuditSubmit)]
    public async Task<ActionResult<AuditSessionDto>> Submit(Guid id, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var access = await _auditService.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        if (!CanEditSession(access, user))
        {
            return Forbid();
        }

        var result = await _auditService.SubmitAuditAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        if (result.MissingItemIds.Count > 0)
        {
            return BadRequest(new
            {
                message = "Missing answers.",
                missingItemIds = result.MissingItemIds
            });
        }

        return result.Session is null ? BadRequest() : Ok(result.Session);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Audit.ViewAny")]
    public async Task<ActionResult<AuditSessionDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var access = await _auditService.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        if (!CanViewSession(access, user))
        {
            return Forbid();
        }

        var result = await _auditService.GetAuditDetailAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = "Audit.ViewAny")]
    public async Task<ActionResult<PaginatedResult<AuditSummaryDto>>> GetAudits(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? brandId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] AuditStatus? status,
        [FromQuery] string? inspectorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!HasPermission(PermissionConstants.AuditViewAll))
        {
            if (user.BranchId.HasValue)
            {
                branchId = user.BranchId.Value;
                inspectorId = null;
            }
            else
            {
                inspectorId = user.Id;
            }
        }

        var query = new AuditQuery(branchId, brandId, from, to, status, page, pageSize, inspectorId);
        var result = await _auditService.GetAuditsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/export")]
    [Authorize(Policy = "Audit.ViewAny")]
    public async Task<IActionResult> ExportAudit(
        Guid id,
        [FromQuery] string? format,
        [FromQuery] string? culture,
        [FromQuery] bool autoPrint = false,
        CancellationToken cancellationToken = default)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var access = await _auditService.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        if (!CanViewSession(access, user))
        {
            return Forbid();
        }

        var exportFormat = ParseExportFormat(format);
        if (exportFormat != AuditExportFormat.Html && !HasPermission(PermissionConstants.ReportExport))
        {
            return Forbid();
        }

        var exported = await _auditService.ExportAuditAsync(id, exportFormat, culture, autoPrint, cancellationToken);
        if (exported is null)
        {
            return NotFound();
        }

        if (exportFormat == AuditExportFormat.Html)
        {
            return Content(Encoding.UTF8.GetString(exported.Content), exported.ContentType);
        }

        return File(exported.Content, exported.ContentType, exported.FileName);
    }

    private bool CanViewSession(AuditSessionAccessDto access, Application.Common.Models.UserInfoDto user)
    {
        if (HasPermission(PermissionConstants.AuditViewAll))
        {
            return true;
        }

        if (access.InspectorId == user.Id)
        {
            return true;
        }

        return user.BranchId.HasValue && access.BranchId == user.BranchId.Value;
    }

    private bool CanEditSession(AuditSessionAccessDto access, Application.Common.Models.UserInfoDto user)
    {
        if (HasPermission(PermissionConstants.AuditViewAll))
        {
            return true;
        }

        return access.InspectorId == user.Id;
    }

    private static AuditExportFormat ParseExportFormat(string? format)
    {
        if (string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return AuditExportFormat.Excel;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return AuditExportFormat.Csv;
        }

        return AuditExportFormat.Html;
    }
}
