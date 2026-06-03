namespace CircleK.QualityAudit.Application.DatabaseMaintenance.Models;

public enum BackupRecordStatus
{
    Success = 1,
    Failed = 2
}

public enum BackupTriggerType
{
    Manual = 1,
    Scheduled = 2
}

public enum BackupScheduleMode
{
    Daily = 1,
    IntervalHours = 2
}

public sealed record DatabaseBackupRecordDto(
    Guid Id,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    DateTime CreatedAtUtc,
    string? RequestedByUserId,
    string? RequestedByDisplayName,
    string? Note,
    BackupTriggerType TriggerType,
    BackupRecordStatus Status,
    string? ErrorMessage);

public sealed record DatabaseBackupScheduleDto(
    Guid Id,
    bool IsEnabled,
    BackupScheduleMode Mode,
    int Hour,
    int Minute,
    int IntervalHours,
    DateTime? LastRunAtUtc,
    DateTime? NextRunAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DatabaseBackupDashboardDto(
    string DatabaseName,
    string BackupRootPath,
    DatabaseBackupScheduleDto Schedule,
    IReadOnlyList<DatabaseBackupRecordDto> Records);

public sealed record CreateDatabaseBackupRequest(string? Note);

public sealed record UpdateDatabaseBackupScheduleRequest(
    bool IsEnabled,
    BackupScheduleMode Mode,
    int Hour,
    int Minute,
    int IntervalHours);

public sealed record RestoreDatabaseBackupResult(bool Success, string? Error);
