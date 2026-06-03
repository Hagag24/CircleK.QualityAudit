namespace CircleK.QualityAudit.Client.Dtos;

public sealed record UserListItemDto(
    string Id,
    string DisplayName,
    string Email,
    string Role,
    Guid? BranchId,
    string? BranchName,
    string? BranchNameAr,
    bool IsActive);

public sealed record UserDetailsDto(
    string Id,
    string DisplayName,
    string Email,
    string Role,
    Guid? BranchId,
    string? BranchName,
    string? BranchNameAr,
    bool IsActive,
    IReadOnlyList<string> Permissions);

public sealed record CreateUserRequest(
    string DisplayName,
    string Email,
    string Password,
    string Role,
    Guid? BranchId,
    bool IsActive);

public sealed record UpdateUserRequest(
    string DisplayName,
    string Email,
    string Role,
    Guid? BranchId,
    bool IsActive);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record UpdatePermissionsRequest(IReadOnlyList<string> Permissions);
