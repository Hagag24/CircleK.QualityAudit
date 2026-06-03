using System.Linq;

namespace CircleK.QualityAudit.Domain.Entities;

public class AuditSection
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int Order { get; set; }

    public AuditTemplate? Template { get; set; }
    public ICollection<AuditItem> Items { get; set; } = new List<AuditItem>();

    public int MaxScore => Items?.Sum(i => i.WOP) ?? 0;
}
