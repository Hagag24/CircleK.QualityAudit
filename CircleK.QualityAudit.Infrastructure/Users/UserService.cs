using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Users.Models;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Infrastructure.Identity;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Users;

public sealed class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserService(
        AppDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                Email = u.Email ?? string.Empty,
                u.BranchId,
                BranchName = u.Branch != null ? u.Branch.Name : null,
                BranchNameAr = u.Branch != null ? u.Branch.NameAr : null,
                u.IsActive
            })
            .ToListAsync(cancellationToken);

        var roleLookup = await BuildRoleLookupAsync(cancellationToken);

        return users
            .Select(u => new UserListItemDto(
                u.Id,
                u.DisplayName,
                u.Email,
                roleLookup.TryGetValue(u.Id, out var role) ? role : string.Empty,
                u.BranchId,
                u.BranchName,
                u.BranchNameAr,
                u.IsActive))
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    public async Task<UserDetailsDto?> GetUserAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var permissions = await _context.UserPermissions
            .AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);

        return new UserDetailsDto(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            role,
            user.BranchId,
            user.Branch?.Name,
            user.Branch?.NameAr,
            user.IsActive,
            permissions);
    }

    public async Task<UserDetailsDto?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = new AppUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            BranchId = request.BranchId,
            IsActive = request.IsActive,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Role) && await _roleManager.RoleExistsAsync(request.Role))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserDetailsDto?> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return null;
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.BranchId = request.BranchId;
        user.IsActive = request.IsActive;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return null;
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            foreach (var role in currentRoles)
            {
                if (role != request.Role)
                {
                    await _userManager.RemoveFromRoleAsync(user, role);
                }
            }

            if (!currentRoles.Contains(request.Role) && await _roleManager.RoleExistsAsync(request.Role))
            {
                await _userManager.AddToRoleAsync(user, request.Role);
            }
        }

        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string id, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return false;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Select(r => r.Name ?? string.Empty)
            .OrderBy(r => r)
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyList<string>)PermissionConstants.All.ToList());

    public async Task<IReadOnlyList<string>> UpdatePermissionsAsync(string id, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Array.Empty<string>();
        }

        var allowed = PermissionConstants.All.ToHashSet();
        var requested = permissions.Where(p => allowed.Contains(p)).Distinct().ToList();

        var existing = await _context.UserPermissions
            .Where(p => p.UserId == id)
            .ToListAsync(cancellationToken);

        _context.UserPermissions.RemoveRange(existing);
        _context.UserPermissions.AddRange(requested.Select(p => new UserPermission
        {
            Id = Guid.NewGuid(),
            UserId = id,
            Permission = p
        }));

        await _context.SaveChangesAsync(cancellationToken);
        return requested;
    }

    private async Task<Dictionary<string, string>> BuildRoleLookupAsync(CancellationToken cancellationToken)
    {
        var roles = await _context.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var roleMap = roles.ToDictionary(r => r.Id, r => r.Name ?? string.Empty);

        var userRoles = await _context.UserRoles.AsNoTracking().ToListAsync(cancellationToken);
        var lookup = new Dictionary<string, string>();

        foreach (var userRole in userRoles)
        {
            if (roleMap.TryGetValue(userRole.RoleId, out var roleName))
            {
                lookup[userRole.UserId] = roleName;
            }
        }

        return lookup;
    }
}
