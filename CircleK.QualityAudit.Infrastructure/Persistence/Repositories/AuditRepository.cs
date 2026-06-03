using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Persistence.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AuditSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.AuditSessions
            .Include(x => x.Answers)
            .ThenInclude(a => a.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddSessionAsync(AuditSession session, CancellationToken cancellationToken = default)
    {
        await _context.AuditSessions.AddAsync(session, cancellationToken);
    }

    public Task AddOrUpdateAnswerAsync(AuditAnswer answer, CancellationToken cancellationToken = default)
    {
        _context.AuditAnswers.Update(answer);
        return Task.CompletedTask;
    }
}
