using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Users.Models;
using CircleK.QualityAudit.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircleK.QualityAudit.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    [Authorize(Policy = PermissionConstants.UserView)]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var result = await _users.GetUsersAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = PermissionConstants.UserView)]
    public async Task<ActionResult<UserDetailsDto>> GetUser(string id, CancellationToken cancellationToken)
    {
        var result = await _users.GetUserAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("roles")]
    [Authorize(Policy = PermissionConstants.UserView)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRoles(CancellationToken cancellationToken)
    {
        var result = await _users.GetRolesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("permissions")]
    [Authorize(Policy = PermissionConstants.UserPermissions)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetPermissions(CancellationToken cancellationToken)
    {
        var result = await _users.GetPermissionsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PermissionConstants.UserCreate)]
    public async Task<ActionResult<UserDetailsDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _users.CreateUserAsync(request, cancellationToken);
        return result is null ? BadRequest("Failed to create user.") : Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = PermissionConstants.UserUpdate)]
    public async Task<ActionResult<UserDetailsDto>> UpdateUser(string id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _users.UpdateUserAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = PermissionConstants.UserResetPassword)]
    public async Task<ActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _users.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
        return result ? Ok() : NotFound();
    }

    [HttpPut("{id}/permissions")]
    [Authorize(Policy = PermissionConstants.UserPermissions)]
    public async Task<ActionResult<IReadOnlyList<string>>> UpdatePermissions(string id, [FromBody] UpdatePermissionsRequest request, CancellationToken cancellationToken)
    {
        var result = await _users.UpdatePermissionsAsync(id, request.Permissions, cancellationToken);
        return Ok(result);
    }
}
