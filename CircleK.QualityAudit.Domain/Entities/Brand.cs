namespace CircleK.QualityAudit.Domain.Entities;

public class Brand
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<AuditTemplate> Templates { get; set; } = new List<AuditTemplate>();
}
