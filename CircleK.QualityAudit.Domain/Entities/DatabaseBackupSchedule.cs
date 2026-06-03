using CircleK.QualityAudit.Domain.Enums;

namespace CircleK.QualityAudit.Domain.Entities;

public class DatabaseBackupSchedule
{
    public Guid Id { get; set; }
    public bool IsEnabled { get; set; }
    public DatabaseBackupScheduleMode Mode { get; set; } = DatabaseBackupScheduleMode.Daily;
    public int Hour { get; set; } = 2;
    public int Minute { get; set; }
    public int IntervalHours { get; set; } = 24;
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
