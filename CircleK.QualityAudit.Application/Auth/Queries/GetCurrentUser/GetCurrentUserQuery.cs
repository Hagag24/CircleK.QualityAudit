using CircleK.QualityAudit.Application.Common.Models;
using MediatR;

namespace CircleK.QualityAudit.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<UserInfoDto?>;
