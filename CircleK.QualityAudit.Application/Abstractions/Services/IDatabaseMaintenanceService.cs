using CircleK.QualityAudit.Application.DatabaseMaintenance.Models;

namespace CircleK.QualityAudit.Application.Abstractions.Services;

public interface IDatabaseMaintenanceService
{
    Task<DatabaseBackupDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<DatabaseBackupRecordDto> CreateBackupAsync(
        string? requestedByUserId,
        string? requestedByDisplayName,
        CreateDatabaseBackupRequest request,
        BackupTriggerType triggerType = BackupTriggerType.Manual,
        CancellationToken cancellationToken = default);
    Task<RestoreDatabaseBackupResult> RestoreBackupAsync(Guid backupId, CancellationToken cancellationToken = default);
    Task<DatabaseBackupScheduleDto> UpdateScheduleAsync(UpdateDatabaseBackupScheduleRequest request, CancellationToken cancellationToken = default);
    Task RunScheduledBackupIfDueAsync(CancellationToken cancellationToken = default);
}
