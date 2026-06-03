using Microsoft.AspNetCore.Identity;

namespace CircleK.QualityAudit.Domain.Entities;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}
