namespace CircleK.QualityAudit.Domain.Entities;

public class AuditItem
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextAr { get; set; }
    public int WOP { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresTiming { get; set; }
    public int Order { get; set; }

    public AuditSection? Section { get; set; }
    public ICollection<AuditAnswer> Answers { get; set; } = new List<AuditAnswer>();
    public ICollection<AuditTimingMeasurement> Timings { get; set; } = new List<AuditTimingMeasurement>();
}
