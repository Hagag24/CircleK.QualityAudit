using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Common.Models;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UserInfoDto?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            var signIn = await _signInManager.PasswordSignInAsync(user, password, false, false);
            if (!signIn.Succeeded)
            {
                return null;
            }

            return await BuildUserInfoAsync(user, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<UserInfoDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var user = await _userManager.GetUserAsync(principal);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            return await BuildUserInfoAsync(user, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<UserInfoDto> BuildUserInfoAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var permissions = await _context.UserPermissions
            .Where(p => p.UserId == user.Id)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);

        return new UserInfoDto(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            role,
            user.BranchId,
            permissions);
    }
}
