namespace CircleK.QualityAudit.Domain.Entities;

public class AuditTimingMeasurement
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ItemId { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }

    public AuditSession? Session { get; set; }
    public AuditItem? Item { get; set; }
}
