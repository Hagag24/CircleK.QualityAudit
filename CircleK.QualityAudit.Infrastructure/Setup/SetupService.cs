using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Setup.Models;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Setup;

public sealed class SetupService : ISetupService
{
    private readonly AppDbContext _context;

    public SetupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Brands
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.NameAr, b.LogoUrl, b.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<BrandDto> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NameAr = request.NameAr?.Trim(),
            LogoUrl = request.LogoUrl?.Trim(),
            IsActive = request.IsActive
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return new BrandDto(brand.Id, brand.Name, brand.NameAr, brand.LogoUrl, brand.IsActive);
    }

    public async Task<BrandDto?> UpdateBrandAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await _context.Brands.FindAsync(new object?[] { id }, cancellationToken);
        if (brand is null)
        {
            return null;
        }

        brand.Name = request.Name.Trim();
        brand.NameAr = request.NameAr?.Trim();
        brand.LogoUrl = request.LogoUrl?.Trim();
        brand.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new BrandDto(brand.Id, brand.Name, brand.NameAr, brand.LogoUrl, brand.IsActive);
    }

    public async Task<bool> DeleteBrandAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var brand = await _context.Brands.FindAsync(new object?[] { id }, cancellationToken);
        if (brand is null)
        {
            return false;
        }

        brand.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? brandId, CancellationToken cancellationToken = default)
    {
        var query = _context.Branches.AsNoTracking();
        if (brandId.HasValue)
        {
            query = query.Where(b => b.BrandId == brandId.Value);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(
                b.Id,
                b.BrandId,
                b.Name,
                b.NameAr,
                b.Region,
                b.ManagerName,
                b.EmployeeCount,
                b.IsActive,
                b.Brand != null ? b.Brand.Name : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<BranchDto?> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await _context.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);

        if (brand is null)
        {
            return null;
        }

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            NameAr = request.NameAr?.Trim(),
            Region = request.Region?.Trim(),
            ManagerName = request.ManagerName?.Trim(),
            EmployeeCount = request.EmployeeCount is { } count && count < 0 ? null : request.EmployeeCount,
            IsActive = request.IsActive
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.BrandId, branch.Name, branch.NameAr, branch.Region, branch.ManagerName, branch.EmployeeCount, branch.IsActive, brand.Name);
    }

    public async Task<BranchDto?> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches.FindAsync(new object?[] { id }, cancellationToken);
        if (branch is null)
        {
            return null;
        }

        var brand = await _context.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);

        if (brand is null)
        {
            return null;
        }

        branch.BrandId = request.BrandId;
        branch.Name = request.Name.Trim();
        branch.NameAr = request.NameAr?.Trim();
        branch.Region = request.Region?.Trim();
        branch.ManagerName = request.ManagerName?.Trim();
        branch.EmployeeCount = request.EmployeeCount is { } count && count < 0 ? null : request.EmployeeCount;
        branch.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.BrandId, branch.Name, branch.NameAr, branch.Region, branch.ManagerName, branch.EmployeeCount, branch.IsActive, brand.Name);
    }

    public async Task<bool> DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches.FindAsync(new object?[] { id }, cancellationToken);
        if (branch is null)
        {
            return false;
        }

        branch.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TemplateListDto>> GetTemplatesAsync(Guid? brandId, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditTemplates.AsNoTracking();
        if (brandId.HasValue)
        {
            query = query.Where(t => t.BrandId == brandId.Value);
        }

        return await query
            .OrderBy(t => t.Name)
            .Select(t => new TemplateListDto(
                t.Id,
                t.BrandId,
                t.Name,
                t.Version,
                t.IsActive,
                t.Sections.Count,
                t.Sections.SelectMany(s => s.Items).Count(),
                t.Sections.SelectMany(s => s.Items).Sum(i => (int?)i.WOP) ?? 0))
            .ToListAsync(cancellationToken);
    }

    public async Task<TemplateDetailsDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditTemplates
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TemplateDetailsDto(
                t.Id,
                t.BrandId,
                t.Name,
                t.Version,
                t.IsActive,
                t.Sections
                    .OrderBy(s => s.Order)
                    .Select(s => new SectionDto(
                        s.Id,
                        s.TemplateId,
                        s.Name,
                        s.NameAr,
                        s.Order,
                        s.Items.Sum(i => (int?)i.WOP) ?? 0,
                        s.Items
                            .OrderBy(i => i.Order)
                            .Select(i => new ItemDto(
                                i.Id,
                                i.SectionId,
                                i.Text,
                                i.TextAr,
                                i.WOP,
                                i.IsCritical,
                                i.RequiresTiming,
                                i.Order))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TemplateListDto?> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var brandExists = await _context.Brands
            .AsNoTracking()
            .AnyAsync(b => b.Id == request.BrandId, cancellationToken);

        if (!brandExists)
        {
            return null;
        }

        var template = new AuditTemplate
        {
            Id = Guid.NewGuid(),
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            Version = request.Version,
            IsActive = request.IsActive
        };

        _context.AuditTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        return new TemplateListDto(template.Id, template.BrandId, template.Name, template.Version, template.IsActive, 0, 0, 0);
    }

    public async Task<TemplateListDto?> UpdateTemplateAsync(Guid id, UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _context.AuditTemplates.FindAsync(new object?[] { id }, cancellationToken);
        if (template is null)
        {
            return null;
        }

        template.Name = request.Name.Trim();
        template.Version = request.Version;
        template.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        var totals = await _context.AuditTemplates
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                SectionCount = t.Sections.Count,
                ItemCount = t.Sections.SelectMany(s => s.Items).Count(),
                TotalWop = t.Sections.SelectMany(s => s.Items).Sum(i => (int?)i.WOP) ?? 0
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new TemplateListDto(template.Id, template.BrandId, template.Name, template.Version, template.IsActive,
            totals?.SectionCount ?? 0,
            totals?.ItemCount ?? 0,
            totals?.TotalWop ?? 0);
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _context.AuditTemplates.FindAsync(new object?[] { id }, cancellationToken);
        if (template is null)
        {
            return false;
        }

        template.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SectionDto?> AddSectionAsync(Guid templateId, CreateSectionRequest request, CancellationToken cancellationToken = default)
    {
        var templateExists = await _context.AuditTemplates
            .AsNoTracking()
            .AnyAsync(t => t.Id == templateId, cancellationToken);

        if (!templateExists)
        {
            return null;
        }

        var order = request.Order ?? ((await _context.AuditSections
            .Where(s => s.TemplateId == templateId)
            .MaxAsync(s => (int?)s.Order, cancellationToken)) ?? 0) + 1;

        var section = new AuditSection
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            Name = request.Name.Trim(),
            NameAr = request.NameAr?.Trim(),
            Order = order
        };

        _context.AuditSections.Add(section);
        await _context.SaveChangesAsync(cancellationToken);

        return new SectionDto(section.Id, section.TemplateId, section.Name, section.NameAr, section.Order, 0, new List<ItemDto>());
    }

    public async Task<SectionDto?> UpdateSectionAsync(Guid id, UpdateSectionRequest request, CancellationToken cancellationToken = default)
    {
        var section = await _context.AuditSections.FindAsync(new object?[] { id }, cancellationToken);
        if (section is null)
        {
            return null;
        }

        section.Name = request.Name.Trim();
        section.NameAr = request.NameAr?.Trim();
        section.Order = request.Order;

        await _context.SaveChangesAsync(cancellationToken);

        var maxScore = await _context.AuditItems
            .AsNoTracking()
            .Where(i => i.SectionId == id)
            .SumAsync(i => (int?)i.WOP, cancellationToken) ?? 0;

        return new SectionDto(section.Id, section.TemplateId, section.Name, section.NameAr, section.Order, maxScore, null);
    }

    public async Task<bool> DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var section = await _context.AuditSections.FindAsync(new object?[] { id }, cancellationToken);
        if (section is null)
        {
            return false;
        }

        _context.AuditSections.Remove(section);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderSectionsAsync(ReorderRequest request, CancellationToken cancellationToken = default)
    {
        var ids = request.Items.Select(i => i.Id).ToList();
        var sections = await _context.AuditSections
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        foreach (var section in sections)
        {
            var item = request.Items.FirstOrDefault(i => i.Id == section.Id);
            if (item is not null)
            {
                section.Order = item.Order;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ItemDto?> AddItemAsync(Guid sectionId, CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        var sectionExists = await _context.AuditSections
            .AsNoTracking()
            .AnyAsync(s => s.Id == sectionId, cancellationToken);

        if (!sectionExists)
        {
            return null;
        }

        var order = request.Order ?? ((await _context.AuditItems
            .Where(i => i.SectionId == sectionId)
            .MaxAsync(i => (int?)i.Order, cancellationToken)) ?? 0) + 1;

        var item = new AuditItem
        {
            Id = Guid.NewGuid(),
            SectionId = sectionId,
            Text = request.Text.Trim(),
            TextAr = request.TextAr?.Trim(),
            WOP = request.Wop,
            IsCritical = request.IsCritical,
            RequiresTiming = request.RequiresTiming,
            Order = order
        };

        _context.AuditItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);

        return new ItemDto(item.Id, item.SectionId, item.Text, item.TextAr, item.WOP, item.IsCritical, item.RequiresTiming, item.Order);
    }

    public async Task<ItemDto?> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _context.AuditItems.FindAsync(new object?[] { id }, cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.Text = request.Text.Trim();
        item.TextAr = request.TextAr?.Trim();
        item.WOP = request.Wop;
        item.IsCritical = request.IsCritical;
        item.RequiresTiming = request.RequiresTiming;
        item.Order = request.Order;

        await _context.SaveChangesAsync(cancellationToken);

        return new ItemDto(item.Id, item.SectionId, item.Text, item.TextAr, item.WOP, item.IsCritical, item.RequiresTiming, item.Order);
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _context.AuditItems.FindAsync(new object?[] { id }, cancellationToken);
        if (item is null)
        {
            return false;
        }

        _context.AuditItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderItemsAsync(ReorderRequest request, CancellationToken cancellationToken = default)
    {
        var ids = request.Items.Select(i => i.Id).ToList();
        var items = await _context.AuditItems
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);

        foreach (var auditItem in items)
        {
            var item = request.Items.FirstOrDefault(i => i.Id == auditItem.Id);
            if (item is not null)
            {
                auditItem.Order = item.Order;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
