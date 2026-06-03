using CircleK.QualityAudit.Application.Common.Models;

namespace CircleK.QualityAudit.Application.Abstractions.Services;

public interface IAuthService
{
    Task<UserInfoDto?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<UserInfoDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
