namespace CircleK.QualityAudit.Application.Setup.Models;

public sealed record BrandDto(
    Guid Id,
    string Name,
    string? NameAr,
    string? LogoUrl,
    bool IsActive);

public sealed record BranchDto(
    Guid Id,
    Guid BrandId,
    string Name,
    string? NameAr,
    string? Region,
    string? ManagerName,
    int? EmployeeCount,
    bool IsActive,
    string? BrandName);

public sealed record TemplateListDto(
    Guid Id,
    Guid BrandId,
    string Name,
    int Version,
    bool IsActive,
    int SectionCount,
    int ItemCount,
    int TotalWop);

public sealed record TemplateDetailsDto(
    Guid Id,
    Guid BrandId,
    string Name,
    int Version,
    bool IsActive,
    IReadOnlyList<SectionDto> Sections);

public sealed record SectionDto(
    Guid Id,
    Guid TemplateId,
    string Name,
    string? NameAr,
    int Order,
    int MaxScore,
    IReadOnlyList<ItemDto>? Items);

public sealed record ItemDto(
    Guid Id,
    Guid SectionId,
    string Text,
    string? TextAr,
    int Wop,
    bool IsCritical,
    bool RequiresTiming,
    int Order);

public sealed record CreateBrandRequest(
    string Name,
    string? NameAr,
    string? LogoUrl,
    bool IsActive);

public sealed record UpdateBrandRequest(
    string Name,
    string? NameAr,
    string? LogoUrl,
    bool IsActive);

public sealed record CreateBranchRequest(
    Guid BrandId,
    string Name,
    string? NameAr,
    string? Region,
    string? ManagerName,
    int? EmployeeCount,
    bool IsActive);

public sealed record UpdateBranchRequest(
    Guid BrandId,
    string Name,
    string? NameAr,
    string? Region,
    string? ManagerName,
    int? EmployeeCount,
    bool IsActive);

public sealed record CreateTemplateRequest(
    Guid BrandId,
    string Name,
    int Version,
    bool IsActive);

public sealed record UpdateTemplateRequest(
    string Name,
    int Version,
    bool IsActive);

public sealed record CreateSectionRequest(
    string Name,
    string? NameAr,
    int? Order);

public sealed record UpdateSectionRequest(
    string Name,
    string? NameAr,
    int Order);

public sealed record CreateItemRequest(
    string Text,
    string? TextAr,
    int Wop,
    bool IsCritical,
    bool RequiresTiming,
    int? Order);

public sealed record UpdateItemRequest(
    string Text,
    string? TextAr,
    int Wop,
    bool IsCritical,
    bool RequiresTiming,
    int Order);

public sealed record ReorderRequest(IReadOnlyList<ReorderItem> Items);

public sealed record ReorderItem(Guid Id, int Order);
