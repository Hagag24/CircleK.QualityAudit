using CircleK.QualityAudit.Application.Abstractions.Services;
using MediatR;

namespace CircleK.QualityAudit.Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAuthService _authService;

    public LogoutCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(cancellationToken);
        return Unit.Value;
    }
}
