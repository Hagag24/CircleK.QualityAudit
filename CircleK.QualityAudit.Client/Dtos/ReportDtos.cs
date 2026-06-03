namespace CircleK.QualityAudit.Client.Dtos;

public sealed record ReportQuery(
    Guid? BrandId,
    DateTime? From,
    DateTime? To,
    string? Search,
    Guid? BranchId = null,
    Guid? AuditId = null);

public sealed record DashboardKpiDto(
    int DangerBranches,
    int WarningBranches,
    int SafeBranches,
    int TotalAudits);

public sealed record BranchRiskRowDto(
    Guid BranchId,
    string BranchName,
    string? BranchNameAr,
    DateTime? LastAuditDate,
    int TotalScore,
    int TotalMax,
    int CriticalFailedCount,
    RiskLevel RiskLevel);

public sealed record SectionAverageDto(
    Guid SectionId,
    string SectionName,
    string? SectionNameAr,
    double AveragePercent);

public sealed record CriticalItemDto(
    Guid ItemId,
    string ItemText,
    string? ItemTextAr,
    int FailedCount);

public sealed record DashboardReportDto(
    DashboardKpiDto Kpis,
    IReadOnlyList<BranchRiskRowDto> Branches,
    IReadOnlyList<SectionAverageDto> Sections,
    IReadOnlyList<CriticalItemDto> CriticalItems);
