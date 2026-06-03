using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.DatabaseMaintenance.Models;
using CircleK.QualityAudit.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircleK.QualityAudit.API.Controllers;

[ApiController]
[Route("api/setup/database")]
[Authorize]
public sealed class DatabaseMaintenanceController : ControllerBase
{
    private readonly IDatabaseMaintenanceService _databaseService;
    private readonly IAuthService _authService;

    public DatabaseMaintenanceController(IDatabaseMaintenanceService databaseService, IAuthService authService)
    {
        _databaseService = databaseService;
        _authService = authService;
    }

    [HttpGet("backups")]
    [Authorize(Policy = PermissionConstants.DatabaseBackupView)]
    public async Task<ActionResult<DatabaseBackupDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.BranchId.HasValue)
        {
            return Forbid();
        }

        var result = await _databaseService.GetDashboardAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("backups")]
    [Authorize(Policy = PermissionConstants.DatabaseBackupCreate)]
    public async Task<ActionResult<DatabaseBackupRecordDto>> CreateBackup([FromBody] CreateDatabaseBackupRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.BranchId.HasValue)
        {
            return Forbid();
        }

        var result = await _databaseService.CreateBackupAsync(user.Id, user.DisplayName, request, BackupTriggerType.Manual, cancellationToken);
        if (result.Status == BackupRecordStatus.Failed)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("backups/{id:guid}/restore")]
    [Authorize(Policy = PermissionConstants.DatabaseBackupRestore)]
    public async Task<ActionResult<RestoreDatabaseBackupResult>> RestoreBackup(Guid id, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.BranchId.HasValue)
        {
            return Forbid();
        }

        var result = await _databaseService.RestoreBackupAsync(id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPut("schedule")]
    [Authorize(Policy = PermissionConstants.DatabaseBackupSchedule)]
    public async Task<ActionResult<DatabaseBackupScheduleDto>> UpdateSchedule([FromBody] UpdateDatabaseBackupScheduleRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.BranchId.HasValue)
        {
            return Forbid();
        }

        var result = await _databaseService.UpdateScheduleAsync(request, cancellationToken);
        return Ok(result);
    }
}
