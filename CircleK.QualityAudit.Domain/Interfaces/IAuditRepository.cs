using CircleK.QualityAudit.Domain.Entities;

namespace CircleK.QualityAudit.Domain.Interfaces;

public interface IAuditRepository
{
    Task<AuditSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddSessionAsync(AuditSession session, CancellationToken cancellationToken = default);
    Task AddOrUpdateAnswerAsync(AuditAnswer answer, CancellationToken cancellationToken = default);
}
