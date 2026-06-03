using CircleK.QualityAudit.Application.Auth.Commands.Login;
using CircleK.QualityAudit.Application.Auth.Commands.Logout;
using CircleK.QualityAudit.Application.Auth.Queries.GetCurrentUser;
using CircleK.QualityAudit.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace CircleK.QualityAudit.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAntiforgery _antiforgery;

    public AuthController(IMediator mediator, IAntiforgery antiforgery)
    {
        _mediator = mediator;
        _antiforgery = antiforgery;
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserInfoDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(), cancellationToken);
        return Ok();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserInfoDto>> Me(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }

        return Ok(result);
    }

    [HttpGet("csrf")]
    public IActionResult CsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}
