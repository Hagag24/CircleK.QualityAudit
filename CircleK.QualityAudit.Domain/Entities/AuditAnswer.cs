using CircleK.QualityAudit.Domain.Enums;

namespace CircleK.QualityAudit.Domain.Entities;

public class AuditAnswer
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ItemId { get; set; }
    public AnswerType Answer { get; set; }
    public int Score { get; set; }
    public string? Notes { get; set; }

    public AuditSession? Session { get; set; }
    public AuditItem? Item { get; set; }
}
