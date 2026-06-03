using CircleK.QualityAudit.Domain.Enums;

namespace CircleK.QualityAudit.Domain.Entities;

public class DatabaseBackupRecord
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? RequestedByUserId { get; set; }
    public string? RequestedByDisplayName { get; set; }
    public string? Note { get; set; }
    public DatabaseBackupTriggerType TriggerType { get; set; }
    public DatabaseBackupStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
