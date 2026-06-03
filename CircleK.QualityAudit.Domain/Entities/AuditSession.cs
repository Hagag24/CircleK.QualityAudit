using CircleK.QualityAudit.Domain.Enums;

namespace CircleK.QualityAudit.Domain.Entities;

public class AuditSession
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid TemplateId { get; set; }
    public string InspectorId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public AuditStatus Status { get; set; } = AuditStatus.Draft;
    public int TotalScore { get; set; }
    public int TotalMax { get; set; }
    public int CriticalFailedCount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? CurrentEmployeeCount { get; set; }

    public Branch? Branch { get; set; }
    public AuditTemplate? Template { get; set; }
    public AppUser? Inspector { get; set; }
    public ICollection<AuditAnswer> Answers { get; set; } = new List<AuditAnswer>();
    public ICollection<AuditTimingMeasurement> Timings { get; set; } = new List<AuditTimingMeasurement>();
}
