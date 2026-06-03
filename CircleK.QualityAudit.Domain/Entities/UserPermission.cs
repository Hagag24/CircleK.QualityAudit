namespace CircleK.QualityAudit.Domain.Entities;

public class UserPermission
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;

    public AppUser? User { get; set; }
}
