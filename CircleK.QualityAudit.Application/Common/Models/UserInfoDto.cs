namespace CircleK.QualityAudit.Application.Common.Models;

public sealed record UserInfoDto(
    string Id,
    string DisplayName,
    string Email,
    string Role,
    Guid? BranchId,
    IReadOnlyList<string> Permissions
);
