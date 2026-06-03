using CircleK.QualityAudit.Domain.Enums;

namespace CircleK.QualityAudit.Application.Audits.Models;

public sealed record CreateAuditRequest(Guid BranchId, Guid TemplateId, DateTime Date, int? CurrentEmployeeCount);

public sealed record SaveAnswerRequest(Guid ItemId, AnswerType Answer, string? Notes);

public sealed record SaveTimingRequest(Guid ItemId, int DurationSeconds);

public sealed record AuditSessionDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string? BranchNameAr,
    Guid TemplateId,
    string TemplateName,
    string InspectorId,
    string InspectorName,
    DateTime Date,
    int? CurrentEmployeeCount,
    AuditStatus Status,
    int TotalScore,
    int TotalMax,
    int CriticalFailedCount,
    DateTime? SubmittedAt,
    RiskLevel RiskLevel);

public sealed record AuditSummaryDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string? BranchNameAr,
    string TemplateName,
    string InspectorName,
    DateTime Date,
    AuditStatus Status,
    int TotalScore,
    int TotalMax,
    int CriticalFailedCount,
    RiskLevel RiskLevel);

public sealed record AuditAnswerDto(
    Guid Id,
    Guid SessionId,
    Guid ItemId,
    AnswerType Answer,
    int Score,
    string? Notes);

public sealed record AuditTimingEntryDto(
    Guid Id,
    int DurationSeconds,
    DateTime RecordedAt);

public sealed record AuditItemTimingDto(
    Guid ItemId,
    int Count,
    double AverageSeconds,
    IReadOnlyList<AuditTimingEntryDto> Entries);

public sealed record AuditItemDto(
    Guid Id,
    string Text,
    string? TextAr,
    int Wop,
    bool IsCritical,
    bool RequiresTiming,
    int Order);

public sealed record AuditSectionDto(
    Guid Id,
    string Name,
    string? NameAr,
    int Order,
    IReadOnlyList<AuditItemDto> Items);

public sealed record AuditFormDto(
    Guid SessionId,
    string TemplateName,
    IReadOnlyList<AuditSectionDto> Sections);

public sealed record AuditSessionDetailDto(
    AuditSessionDto Session,
    IReadOnlyList<AuditAnswerDto> Answers,
    IReadOnlyList<AuditItemTimingDto> Timings);

public sealed record SubmitAuditResult(
    AuditSessionDto? Session,
    IReadOnlyList<Guid> MissingItemIds);

public sealed record AuditSessionAccessDto(
    Guid SessionId,
    Guid BranchId,
    string InspectorId,
    AuditStatus Status);

public sealed record AuditQuery(
    Guid? BranchId,
    Guid? BrandId,
    DateTime? From,
    DateTime? To,
    AuditStatus? Status,
    int Page = 1,
    int PageSize = 20,
    string? InspectorId = null);

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public enum AuditExportFormat
{
    Html = 1,
    Excel = 2,
    Csv = 3
}

public sealed record AuditExportResult(
    byte[] Content,
    string FileName,
    string ContentType);
