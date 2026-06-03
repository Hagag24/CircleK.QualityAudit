using CircleK.QualityAudit.Application.Users.Models;

namespace CircleK.QualityAudit.Application.Abstractions.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDetailsDto?> GetUserAsync(string id, CancellationToken cancellationToken = default);
    Task<UserDetailsDto?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDetailsDto?> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(string id, string newPassword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> UpdatePermissionsAsync(string id, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);
}
