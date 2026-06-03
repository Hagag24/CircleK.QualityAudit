using CircleK.QualityAudit.Application.Setup.Models;

namespace CircleK.QualityAudit.Application.Abstractions.Services;

public interface ISetupService
{
    Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<BrandDto> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<BrandDto?> UpdateBrandAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteBrandAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? brandId, CancellationToken cancellationToken = default);
    Task<BranchDto?> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto?> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TemplateListDto>> GetTemplatesAsync(Guid? brandId, CancellationToken cancellationToken = default);
    Task<TemplateDetailsDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TemplateListDto?> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TemplateListDto?> UpdateTemplateAsync(Guid id, UpdateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SectionDto?> AddSectionAsync(Guid templateId, CreateSectionRequest request, CancellationToken cancellationToken = default);
    Task<SectionDto?> UpdateSectionAsync(Guid id, UpdateSectionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReorderSectionsAsync(ReorderRequest request, CancellationToken cancellationToken = default);

    Task<ItemDto?> AddItemAsync(Guid sectionId, CreateItemRequest request, CancellationToken cancellationToken = default);
    Task<ItemDto?> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReorderItemsAsync(ReorderRequest request, CancellationToken cancellationToken = default);
}
