namespace CircleK.QualityAudit.Domain.Entities;

public class AuditTemplate
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public Brand? Brand { get; set; }
    public ICollection<AuditSection> Sections { get; set; } = new List<AuditSection>();
}
