namespace CircleK.QualityAudit.Client.Auth;

public sealed record UserInfoDto(
    string Id,
    string DisplayName,
    string Email,
    string Role,
    Guid? BranchId,
    IReadOnlyList<string> Permissions
);
