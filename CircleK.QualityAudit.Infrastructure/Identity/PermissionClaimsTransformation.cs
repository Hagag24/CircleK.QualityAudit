using System.Security.Claims;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Identity;

public sealed class PermissionClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _context;

    public PermissionClaimsTransformation(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null || !identity.IsAuthenticated)
        {
            return principal;
        }

        if (identity.HasClaim(c => c.Type == "permission"))
        {
            return principal;
        }

        var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return principal;
        }

        var permissions = await _context.UserPermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission)
            .ToListAsync();

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        return principal;
    }
}
