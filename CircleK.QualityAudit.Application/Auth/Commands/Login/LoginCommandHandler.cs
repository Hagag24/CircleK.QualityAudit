using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Common.Models;
using MediatR;

namespace CircleK.QualityAudit.Application.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, UserInfoDto?>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<UserInfoDto?> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        _authService.LoginAsync(request.Email, request.Password, cancellationToken);
}
