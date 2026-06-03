namespace CircleK.QualityAudit.Domain.Entities;

public class Branch
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Region { get; set; }
    public string? ManagerName { get; set; }
    public int? EmployeeCount { get; set; }
    public bool IsActive { get; set; } = true;

    public Brand? Brand { get; set; }
    public ICollection<AuditSession> AuditSessions { get; set; } = new List<AuditSession>();
}
