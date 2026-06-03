using CircleK.QualityAudit.Application.Audits.Models;

namespace CircleK.QualityAudit.Application.Abstractions.Services;

public interface IAuditService
{
    Task<AuditSessionDto?> CreateAuditAsync(CreateAuditRequest request, string inspectorId, CancellationToken cancellationToken = default);
    Task<AuditFormDto?> GetAuditFormAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<AuditAnswerDto?> SaveAnswerAsync(Guid sessionId, SaveAnswerRequest request, CancellationToken cancellationToken = default);
    Task<AuditItemTimingDto?> AddTimingAsync(Guid sessionId, SaveTimingRequest request, CancellationToken cancellationToken = default);
    Task<SubmitAuditResult?> SubmitAuditAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<AuditSessionAccessDto?> GetAccessInfoAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<AuditSessionDetailDto?> GetAuditDetailAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<PaginatedResult<AuditSummaryDto>> GetAuditsAsync(AuditQuery query, CancellationToken cancellationToken = default);
    Task<AuditExportResult?> ExportAuditAsync(
        Guid sessionId,
        AuditExportFormat format,
        string? cultureName,
        bool autoPrint,
        CancellationToken cancellationToken = default);
}
