using CircleK.QualityAudit.Application.Common.Models;
using MediatR;

namespace CircleK.QualityAudit.Application.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<UserInfoDto?>;
