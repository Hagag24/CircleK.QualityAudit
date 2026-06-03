using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Setup.Models;
using CircleK.QualityAudit.Infrastructure.Identity;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.API.Controllers;

[ApiController]
[Route("api/setup")]
[Authorize]
public sealed class SetupController : ControllerBase
{
    private readonly ISetupService _setupService;
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;

    public SetupController(ISetupService setupService, IAuthService authService, AppDbContext context)
    {
        _setupService = setupService;
        _authService = authService;
        _context = context;
    }

    private async Task<(Guid? BranchId, Guid? BrandId)?> GetUserScopeAsync(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        Guid? brandId = null;
        if (user.BranchId.HasValue)
        {
            brandId = await _context.Branches
                .AsNoTracking()
                .Where(b => b.Id == user.BranchId.Value)
                .Select(b => (Guid?)b.BrandId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return (user.BranchId, brandId);
    }

    private async Task<Guid?> GetBrandScopeAsync(CancellationToken cancellationToken)
    {
        var scope = await GetUserScopeAsync(cancellationToken);
        return scope?.BrandId;
    }

    private async Task<bool> IsBrandInScopeAsync(Guid brandId, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        return !scopeBrandId.HasValue || scopeBrandId.Value == brandId;
    }

    private async Task<bool> IsBranchInScopeAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        if (!scopeBrandId.HasValue)
        {
            return true;
        }

        var brandId = await _context.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.BrandId)
            .FirstOrDefaultAsync(cancellationToken);

        return brandId == scopeBrandId.Value;
    }

    private async Task<bool> IsTemplateInScopeAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        if (!scopeBrandId.HasValue)
        {
            return true;
        }

        var brandId = await _context.AuditTemplates
            .AsNoTracking()
            .Where(t => t.Id == templateId)
            .Select(t => t.BrandId)
            .FirstOrDefaultAsync(cancellationToken);

        return brandId == scopeBrandId.Value;
    }

    private async Task<bool> IsSectionInScopeAsync(Guid sectionId, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        if (!scopeBrandId.HasValue)
        {
            return true;
        }

        var brandId = await _context.AuditSections
            .AsNoTracking()
            .Where(s => s.Id == sectionId)
            .Select(s => s.Template!.BrandId)
            .FirstOrDefaultAsync(cancellationToken);

        return brandId == scopeBrandId.Value;
    }

    private async Task<bool> IsItemInScopeAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        if (!scopeBrandId.HasValue)
        {
            return true;
        }

        var brandId = await _context.AuditItems
            .AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => i.Section!.Template!.BrandId)
            .FirstOrDefaultAsync(cancellationToken);

        return brandId == scopeBrandId.Value;
    }

    private async Task<bool> AreSectionsInScopeAsync(IReadOnlyList<ReorderItem> items, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        if (!scopeBrandId.HasValue || items.Count == 0)
        {
            return true;
        }

        var ids = items.Select(i => i.Id).ToList();
        var hasOutOfScope = await _context.AuditSections
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id) && s.Template!.BrandId != scopeBrandId.Value)
            .AnyAsync(cancellationToken);

        return !hasOutOfScope;
    }

    private async Task<bool> AreItemsInScopeAsync(IReadOnlyList<ReorderItem> items, CancellationToken cancellationToken)
    {
        var scopeBrandId = await GetBrandScopeAsync(cancellationToken);
        if (!scopeBrandId.HasValue || items.Count == 0)
        {
            return true;
        }

        var ids = items.Select(i => i.Id).ToList();
        var hasOutOfScope = await _context.AuditItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id) && i.Section!.Template!.BrandId != scopeBrandId.Value)
            .AnyAsync(cancellationToken);

        return !hasOutOfScope;
    }

    [HttpGet("brands")]
    [Authorize(Policy = PermissionConstants.BrandView)]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> GetBrands(CancellationToken cancellationToken)
    {
        var result = await _setupService.GetBrandsAsync(cancellationToken);

        var scope = await GetUserScopeAsync(cancellationToken);
        if (scope is null)
        {
            return Unauthorized();
        }

        if (!scope.Value.BrandId.HasValue)
        {
            return Ok(result);
        }

        return Ok(result.Where(b => b.Id == scope.Value.BrandId.Value).ToList());
    }

    [HttpPost("brands")]
    [Authorize(Policy = PermissionConstants.BrandCreate)]
    public async Task<ActionResult<BrandDto>> CreateBrand([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        var scope = await GetUserScopeAsync(cancellationToken);
        if (scope is null)
        {
            return Unauthorized();
        }

        if (scope.Value.BranchId.HasValue)
        {
            return Forbid();
        }

        var result = await _setupService.CreateBrandAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("brands/{id:guid}")]
    [Authorize(Policy = PermissionConstants.BrandUpdate)]
    public async Task<ActionResult<BrandDto>> UpdateBrand(Guid id, [FromBody] UpdateBrandRequest request, CancellationToken cancellationToken)
    {
        if (!await IsBrandInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.UpdateBrandAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("brands/{id:guid}")]
    [Authorize(Policy = PermissionConstants.BrandDelete)]
    public async Task<IActionResult> DeleteBrand(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsBrandInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var removed = await _setupService.DeleteBrandAsync(id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpGet("branches")]
    [Authorize(Policy = PermissionConstants.BranchView)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches([FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        var scope = await GetUserScopeAsync(cancellationToken);
        if (scope is null)
        {
            return Unauthorized();
        }

        if (!scope.Value.BranchId.HasValue)
        {
            var result = await _setupService.GetBranchesAsync(brandId, cancellationToken);
            return Ok(result);
        }

        var branches = await _setupService.GetBranchesAsync(scope.Value.BrandId, cancellationToken);
        return Ok(branches.Where(b => b.Id == scope.Value.BranchId.Value).ToList());
    }

    [HttpPost("branches")]
    [Authorize(Policy = PermissionConstants.BranchCreate)]
    public async Task<ActionResult<BranchDto>> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        if (!await IsBrandInScopeAsync(request.BrandId, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.CreateBranchAsync(request, cancellationToken);
        return result is null ? BadRequest("Invalid brand id.") : Ok(result);
    }

    [HttpPut("branches/{id:guid}")]
    [Authorize(Policy = PermissionConstants.BranchUpdate)]
    public async Task<ActionResult<BranchDto>> UpdateBranch(Guid id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        if (!await IsBranchInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        if (!await IsBrandInScopeAsync(request.BrandId, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.UpdateBranchAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("branches/{id:guid}")]
    [Authorize(Policy = PermissionConstants.BranchDelete)]
    public async Task<IActionResult> DeleteBranch(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsBranchInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var removed = await _setupService.DeleteBranchAsync(id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpGet("templates")]
    [Authorize(Policy = PermissionConstants.TemplateView)]
    public async Task<ActionResult<IReadOnlyList<TemplateListDto>>> GetTemplates([FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        var scope = await GetUserScopeAsync(cancellationToken);
        if (scope is null)
        {
            return Unauthorized();
        }

        if (!scope.Value.BrandId.HasValue)
        {
            var result = await _setupService.GetTemplatesAsync(brandId, cancellationToken);
            return Ok(result);
        }

        var templates = await _setupService.GetTemplatesAsync(scope.Value.BrandId.Value, cancellationToken);
        return Ok(templates);
    }

    [HttpGet("templates/{id:guid}")]
    [Authorize(Policy = PermissionConstants.TemplateView)]
    public async Task<ActionResult<TemplateDetailsDto>> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _setupService.GetTemplateAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var scope = await GetUserScopeAsync(cancellationToken);
        if (scope is null)
        {
            return Unauthorized();
        }

        if (!scope.Value.BrandId.HasValue)
        {
            return Ok(result);
        }

        if (result.BrandId != scope.Value.BrandId.Value)
        {
            return Forbid();
        }

        return Ok(result);
    }

    [HttpPost("templates")]
    [Authorize(Policy = PermissionConstants.TemplateCreate)]
    public async Task<ActionResult<TemplateListDto>> CreateTemplate([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!await IsBrandInScopeAsync(request.BrandId, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.CreateTemplateAsync(request, cancellationToken);
        return result is null ? BadRequest("Invalid brand id.") : Ok(result);
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize(Policy = PermissionConstants.TemplateUpdate)]
    public async Task<ActionResult<TemplateListDto>> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!await IsTemplateInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.UpdateTemplateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("templates/{id:guid}")]
    [Authorize(Policy = PermissionConstants.TemplateDelete)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsTemplateInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var removed = await _setupService.DeleteTemplateAsync(id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpPost("templates/{templateId:guid}/sections")]
    [Authorize(Policy = PermissionConstants.SectionCreate)]
    public async Task<ActionResult<SectionDto>> AddSection(Guid templateId, [FromBody] CreateSectionRequest request, CancellationToken cancellationToken)
    {
        if (!await IsTemplateInScopeAsync(templateId, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.AddSectionAsync(templateId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("sections/{id:guid}")]
    [Authorize(Policy = PermissionConstants.SectionUpdate)]
    public async Task<ActionResult<SectionDto>> UpdateSection(Guid id, [FromBody] UpdateSectionRequest request, CancellationToken cancellationToken)
    {
        if (!await IsSectionInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.UpdateSectionAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("sections/{id:guid}")]
    [Authorize(Policy = PermissionConstants.SectionDelete)]
    public async Task<IActionResult> DeleteSection(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsSectionInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var removed = await _setupService.DeleteSectionAsync(id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpPut("sections/reorder")]
    [Authorize(Policy = PermissionConstants.SectionUpdate)]
    public async Task<IActionResult> ReorderSections([FromBody] ReorderRequest request, CancellationToken cancellationToken)
    {
        if (!await AreSectionsInScopeAsync(request.Items, cancellationToken))
        {
            return Forbid();
        }

        await _setupService.ReorderSectionsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("sections/{sectionId:guid}/items")]
    [Authorize(Policy = PermissionConstants.ItemCreate)]
    public async Task<ActionResult<ItemDto>> AddItem(Guid sectionId, [FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (!await IsSectionInScopeAsync(sectionId, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.AddItemAsync(sectionId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("items/{id:guid}")]
    [Authorize(Policy = PermissionConstants.ItemUpdate)]
    public async Task<ActionResult<ItemDto>> UpdateItem(Guid id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        if (!await IsItemInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _setupService.UpdateItemAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("items/{id:guid}")]
    [Authorize(Policy = PermissionConstants.ItemDelete)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsItemInScopeAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var removed = await _setupService.DeleteItemAsync(id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpPut("items/reorder")]
    [Authorize(Policy = PermissionConstants.ItemUpdate)]
    public async Task<IActionResult> ReorderItems([FromBody] ReorderRequest request, CancellationToken cancellationToken)
    {
        if (!await AreItemsInScopeAsync(request.Items, cancellationToken))
        {
            return Forbid();
        }

        await _setupService.ReorderItemsAsync(request, cancellationToken);
        return NoContent();
    }
}
