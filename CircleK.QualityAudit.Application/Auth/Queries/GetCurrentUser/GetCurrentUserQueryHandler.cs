using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Common.Models;
using MediatR;

namespace CircleK.QualityAudit.Application.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserInfoDto?>
{
    private readonly IAuthService _authService;

    public GetCurrentUserQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<UserInfoDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        _authService.GetCurrentUserAsync(cancellationToken);
}
