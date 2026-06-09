using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Audits.Models;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Domain.Enums;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Audits;

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AuditSessionDto?> CreateAuditAsync(CreateAuditRequest request, string inspectorId, CancellationToken cancellationToken = default)
    {
        // Combine queries to reduce database round trips
        var branchTask = _context.Branches
            .AsNoTracking()
            .Where(b => b.Id == request.BranchId && b.IsActive)
            .Select(b => new { b.Name, b.NameAr })
            .FirstOrDefaultAsync(cancellationToken);
        
        var templateTask = _context.AuditTemplates
            .AsNoTracking()
            .Where(t => t.Id == request.TemplateId && t.IsActive)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);
        
        var inspectorTask = _context.Users
            .AsNoTracking()
            .Where(u => u.Id == inspectorId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        await Task.WhenAll(branchTask, templateTask, inspectorTask).ConfigureAwait(false);

        var branch = await branchTask;
        var template = await templateTask;
        var inspectorName = await inspectorTask;

        if (branch is null || template is null)
        {
            return null;
        }

        var session = new AuditSession
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            TemplateId = request.TemplateId,
            InspectorId = inspectorId,
            Date = request.Date,
            Status = AuditStatus.Draft,
            TotalScore = 0,
            TotalMax = 0,
            CriticalFailedCount = 0,
            CurrentEmployeeCount = request.CurrentEmployeeCount
        };

        _context.AuditSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapSessionDto(session, branch.Name, branch.NameAr, template, inspectorName ?? string.Empty);
    }

    public async Task<AuditFormDto?> GetAuditFormAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Use Include to load related data in a single query
        var session = await _context.AuditSessions
            .AsNoTracking()
            .Include(s => s.Template)
                .ThenInclude(t => t!.Sections)
                    .ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session?.Template is null)
        {
            return null;
        }

        return new AuditFormDto(
            sessionId,
            session.Template.Name,
            session.Template.Sections
                .OrderBy(s => s.Order)
                .Select(s => new AuditSectionDto(
                    s.Id,
                    s.Name,
                    s.NameAr,
                    s.Order,
                    s.Items
                        .OrderBy(i => i.Order)
                        .Select(i => new AuditItemDto(
                            i.Id,
                            i.Text,
                            i.TextAr,
                            i.WOP,
                            i.IsCritical,
                            i.RequiresTiming,
                            i.Order))
                        .ToList()))
                .ToList());
    }

    public async Task<AuditAnswerDto?> SaveAnswerAsync(Guid sessionId, SaveAnswerRequest request, CancellationToken cancellationToken = default)
    {
        // Combine session and item queries
        var sessionTask = _context.AuditSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Status, s.TemplateId })
            .FirstOrDefaultAsync(cancellationToken);
        
        var itemTask = _context.AuditItems
            .Include(i => i.Section)
            .Where(i => i.Id == request.ItemId)
            .FirstOrDefaultAsync(cancellationToken);

        await Task.WhenAll(sessionTask, itemTask).ConfigureAwait(false);

        var session = await sessionTask;
        var item = await itemTask;

        if (session is null || session.Status == AuditStatus.Submitted)
        {
            return null;
        }

        if (item is null || item.Section is null || item.Section.TemplateId != session.TemplateId)
        {
            return null;
        }

        var answer = await _context.AuditAnswers
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.ItemId == request.ItemId, cancellationToken)
            .ConfigureAwait(false);

        if (answer is null)
        {
            answer = new AuditAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                ItemId = request.ItemId
            };

            _context.AuditAnswers.Add(answer);
        }

        answer.Answer = request.Answer;
        answer.Score = request.Answer == AnswerType.Yes ? item.WOP : 0;
        answer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AuditAnswerDto(answer.Id, answer.SessionId, answer.ItemId, answer.Answer, answer.Score, answer.Notes);
    }

    public async Task<AuditItemTimingDto?> AddTimingAsync(Guid sessionId, SaveTimingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DurationSeconds <= 0)
        {
            return null;
        }

        // Combine session and item queries
        var sessionTask = _context.AuditSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Status, s.TemplateId })
            .FirstOrDefaultAsync(cancellationToken);
        
        var itemTask = _context.AuditItems
            .Include(i => i.Section)
            .Where(i => i.Id == request.ItemId)
            .FirstOrDefaultAsync(cancellationToken);

        await Task.WhenAll(sessionTask, itemTask).ConfigureAwait(false);

        var session = await sessionTask;
        var item = await itemTask;

        if (session is null || session.Status == AuditStatus.Submitted)
        {
            return null;
        }

        if (item is null || item.Section is null || item.Section.TemplateId != session.TemplateId)
        {
            return null;
        }

        var measurement = new AuditTimingMeasurement
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ItemId = request.ItemId,
            DurationSeconds = request.DurationSeconds,
            RecordedAt = DateTime.UtcNow
        };

        _context.AuditTimingMeasurements.Add(measurement);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var entries = await _context.AuditTimingMeasurements
            .AsNoTracking()
            .Where(t => t.SessionId == sessionId && t.ItemId == request.ItemId)
            .OrderBy(t => t.RecordedAt)
            .Select(t => new AuditTimingEntryDto(t.Id, t.DurationSeconds, t.RecordedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return MapItemTiming(request.ItemId, entries);
    }

    public async Task<SubmitAuditResult?> SubmitAuditAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.AuditSessions
            .Include(s => s.Branch)
            .Include(s => s.Template)
            .Include(s => s.Inspector)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return null;
        }

        if (session.Status == AuditStatus.Submitted)
        {
            var submittedDto = MapSessionDto(
                session,
                session.Branch?.Name ?? string.Empty,
                session.Branch?.NameAr,
                session.Template?.Name ?? string.Empty,
                session.Inspector?.DisplayName ?? string.Empty);

            return new SubmitAuditResult(submittedDto, Array.Empty<Guid>());
        }

        var template = await _context.AuditTemplates
            .Include(t => t.Sections)
            .ThenInclude(s => s.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == session.TemplateId, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return null;
        }

        var answers = await _context.AuditAnswers
            .Where(a => a.SessionId == sessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var answerLookup = answers.ToDictionary(a => a.ItemId, a => a);
        var allItems = template.Sections.SelectMany(s => s.Items).ToList();

        var missing = allItems
            .Where(i => !answerLookup.ContainsKey(i.Id))
            .Select(i => i.Id)
            .ToList();

        if (missing.Count > 0)
        {
            return new SubmitAuditResult(null, missing);
        }

        var totalScore = 0;
        var totalMax = 0;
        var criticalFailed = 0;

        foreach (var item in allItems)
        {
            if (!answerLookup.TryGetValue(item.Id, out var answer))
            {
                continue;
            }

            var score = answer.Answer == AnswerType.Yes ? item.WOP : 0;
            if (answer.Score != score)
            {
                answer.Score = score;
            }

            if (answer.Answer != AnswerType.NA)
            {
                totalMax += item.WOP;
                totalScore += score;
            }

            if (item.IsCritical && answer.Answer == AnswerType.No)
            {
                criticalFailed++;
            }
        }

        session.TotalScore = totalScore;
        session.TotalMax = totalMax;
        session.CriticalFailedCount = criticalFailed;
        session.Status = AuditStatus.Submitted;
        session.SubmittedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = MapSessionDto(
            session,
            session.Branch?.Name ?? string.Empty,
            session.Branch?.NameAr,
            template.Name,
            session.Inspector?.DisplayName ?? string.Empty);

        return new SubmitAuditResult(dto, Array.Empty<Guid>());
    }

    public async Task<AuditSessionAccessDto?> GetAccessInfoAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.AuditSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new AuditSessionAccessDto(
                s.Id,
                s.BranchId,
                s.InspectorId,
                s.Status))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AuditSessionDetailDto?> GetAuditDetailAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.AuditSessions
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.Template)
            .Include(s => s.Inspector)
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return null;
        }

        var sessionDto = MapSessionDto(
            session,
            session.Branch?.Name ?? string.Empty,
            session.Branch?.NameAr,
            session.Template?.Name ?? string.Empty,
            session.Inspector?.DisplayName ?? string.Empty);

        var answers = session.Answers
            .Select(a => new AuditAnswerDto(a.Id, a.SessionId, a.ItemId, a.Answer, a.Score, a.Notes))
            .ToList();

        var timingEntries = await _context.AuditTimingMeasurements
            .AsNoTracking()
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.RecordedAt)
            .Select(t => new
            {
                t.ItemId,
                Entry = new AuditTimingEntryDto(t.Id, t.DurationSeconds, t.RecordedAt)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var timings = timingEntries
            .GroupBy(t => t.ItemId)
            .Select(g => MapItemTiming(g.Key, g.Select(x => x.Entry).ToList()))
            .ToList();

        return new AuditSessionDetailDto(sessionDto, answers, timings);
    }

    public async Task<PaginatedResult<AuditSummaryDto>> GetAuditsAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var auditsQuery = _context.AuditSessions
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.Template)
            .Include(s => s.Inspector)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.InspectorId))
        {
            auditsQuery = auditsQuery.Where(s => s.InspectorId == query.InspectorId);
        }

        if (query.BranchId.HasValue)
        {
            auditsQuery = auditsQuery.Where(s => s.BranchId == query.BranchId.Value);
        }

        if (query.BrandId.HasValue)
        {
            auditsQuery = auditsQuery.Where(s => s.Branch != null && s.Branch.BrandId == query.BrandId.Value);
        }

        if (query.From.HasValue)
        {
            var fromDate = query.From.Value.Date;
            auditsQuery = auditsQuery.Where(s => s.Date >= fromDate);
        }

        if (query.To.HasValue)
        {
            // Treat "to" as end-of-day so date-only filters include the entire selected day.
            var toExclusive = query.To.Value.Date.AddDays(1);
            auditsQuery = auditsQuery.Where(s => s.Date < toExclusive);
        }

        if (query.Status.HasValue)
        {
            auditsQuery = auditsQuery.Where(s => s.Status == query.Status.Value);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var totalCount = await auditsQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await auditsQuery
            .OrderByDescending(s => s.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.BranchId,
                BranchName = s.Branch != null ? s.Branch.Name : string.Empty,
                BranchNameAr = s.Branch != null ? s.Branch.NameAr : null,
                TemplateName = s.Template != null ? s.Template.Name : string.Empty,
                InspectorName = s.Inspector != null ? s.Inspector.DisplayName : string.Empty,
                s.Date,
                s.Status,
                s.TotalScore,
                s.TotalMax,
                s.CriticalFailedCount
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summaries = items
            .Select(s => new AuditSummaryDto(
                s.Id,
                s.BranchId,
                s.BranchName,
                s.BranchNameAr,
                s.TemplateName,
                s.InspectorName,
                s.Date,
                s.Status,
                s.TotalScore,
                s.TotalMax,
                s.CriticalFailedCount,
                ComputeRisk(s.TotalScore, s.TotalMax, s.CriticalFailedCount)))
            .ToList();

        return new PaginatedResult<AuditSummaryDto>(summaries, page, pageSize, totalCount);
    }

    public async Task<AuditExportResult?> ExportAuditAsync(
        Guid sessionId,
        AuditExportFormat format,
        string? cultureName,
        bool autoPrint,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetAuditDetailAsync(sessionId, cancellationToken);
        var form = await GetAuditFormAsync(sessionId, cancellationToken);
        if (detail is null || form is null)
        {
            return null;
        }

        var isArabic = cultureName?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ?? false;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = format switch
        {
            AuditExportFormat.Excel => $"CircleK-Audit-{detail.Session.Id:N}-{stamp}.xlsx",
            AuditExportFormat.Csv => $"CircleK-Audit-{detail.Session.Id:N}-{stamp}.csv",
            _ => $"CircleK-Audit-{detail.Session.Id:N}-{stamp}.html"
        };

        var contentType = format switch
        {
            AuditExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            AuditExportFormat.Csv => "text/csv",
            _ => "text/html"
        };

        // TODO: Implement actual export functionality
        var content = System.Text.Encoding.UTF8.GetBytes($"Export for audit {detail.Session.Id}");
        return new AuditExportResult(content, fileName, contentType);
    }

    private static AuditSessionDto MapSessionDto(
        AuditSession session,
        string branchName,
        string? branchNameAr,
        string templateName,
        string inspectorName)
    {
        return new AuditSessionDto(
            session.Id,
            session.BranchId,
            branchName,
            branchNameAr,
            session.TemplateId,
            templateName,
            session.InspectorId,
            inspectorName,
            session.Date,
            session.CurrentEmployeeCount,
            session.Status,
            session.TotalScore,
            session.TotalMax,
            session.CriticalFailedCount,
            session.SubmittedAt,
            ComputeRisk(session.TotalScore, session.TotalMax, session.CriticalFailedCount));
    }

    private static RiskLevel ComputeRisk(int totalScore, int totalMax, int criticalFailed)
    {
        if (totalMax == 0) return RiskLevel.Safe;
        
        var percentage = (double)totalScore / totalMax * 100;
        
        if (criticalFailed > 0) return RiskLevel.Danger;
        if (percentage < 70) return RiskLevel.Warning;
        return RiskLevel.Safe;
    }

    private static AuditItemTimingDto MapItemTiming(Guid itemId, IReadOnlyList<AuditTimingEntryDto> entries)
    {
        var count = entries.Count;
        var average = count > 0 ? entries.Average(e => e.DurationSeconds) : 0;

        return new AuditItemTimingDto(itemId, count, average, entries);
    }
}

