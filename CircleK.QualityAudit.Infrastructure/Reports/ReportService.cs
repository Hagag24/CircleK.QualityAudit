using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Reports.Models;
using CircleK.QualityAudit.Domain.Enums;
using CircleK.QualityAudit.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardReportDto> GetDashboardAsync(ReportQuery query, CancellationToken cancellationToken = default)
    {
        var sessions = await GetSessionSnapshotsAsync(query, cancellationToken);
        var branchRows = BuildBranchRiskRows(sessions);

        var kpis = new DashboardKpiDto(
            branchRows.Count(r => r.RiskLevel == RiskLevel.Danger),
            branchRows.Count(r => r.RiskLevel == RiskLevel.Warning),
            branchRows.Count(r => r.RiskLevel == RiskLevel.Safe),
            sessions.Count);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var sections = await GetSectionAveragesAsync(sessionIds, cancellationToken);
        var criticalItems = await GetCriticalItemsAsync(sessionIds, cancellationToken);

        return new DashboardReportDto(kpis, branchRows, sections, criticalItems);
    }

    public async Task<IReadOnlyList<BranchRiskRowDto>> GetBranchScoresAsync(ReportQuery query, CancellationToken cancellationToken = default)
    {
        var sessions = await GetSessionSnapshotsAsync(query, cancellationToken);
        return BuildBranchRiskRows(sessions);
    }

    public async Task<IReadOnlyList<CriticalItemDto>> GetCriticalItemsAsync(ReportQuery query, CancellationToken cancellationToken = default)
    {
        var sessions = await GetSessionSnapshotsAsync(query, cancellationToken);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        return await GetCriticalItemsAsync(sessionIds, cancellationToken);
    }

    public async Task<ReportExportResult?> ExportAsync(ReportQuery query, CancellationToken cancellationToken = default)
    {
        var dashboard = await GetDashboardAsync(query, cancellationToken);

        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "KPI";
        summary.Cell(1, 2).Value = "Value";
        summary.Cell(2, 1).Value = "Danger Branches";
        summary.Cell(2, 2).Value = dashboard.Kpis.DangerBranches;
        summary.Cell(3, 1).Value = "Warning Branches";
        summary.Cell(3, 2).Value = dashboard.Kpis.WarningBranches;
        summary.Cell(4, 1).Value = "Safe Branches";
        summary.Cell(4, 2).Value = dashboard.Kpis.SafeBranches;
        summary.Cell(5, 1).Value = "Total Audits";
        summary.Cell(5, 2).Value = dashboard.Kpis.TotalAudits;
        summary.Range(1, 1, 1, 2).Style.Font.Bold = true;
        summary.Columns().AdjustToContents();

        var branches = workbook.Worksheets.Add("Branch Risk");
        branches.Cell(1, 1).Value = "Branch";
        branches.Cell(1, 2).Value = "Last Audit";
        branches.Cell(1, 3).Value = "Score";
        branches.Cell(1, 4).Value = "Critical Failed";
        branches.Cell(1, 5).Value = "Risk";
        var row = 2;
        foreach (var item in dashboard.Branches)
        {
            branches.Cell(row, 1).Value = item.BranchName;
            branches.Cell(row, 2).Value = item.LastAuditDate?.ToString("yyyy-MM-dd");
            branches.Cell(row, 3).Value = item.TotalMax > 0 ? $"{Math.Round(item.TotalScore / (double)item.TotalMax * 100)}%" : "0%";
            branches.Cell(row, 4).Value = item.CriticalFailedCount;
            branches.Cell(row, 5).Value = item.RiskLevel.ToString();
            row++;
        }
        branches.Range(1, 1, 1, 5).Style.Font.Bold = true;
        branches.Columns().AdjustToContents();

        var critical = workbook.Worksheets.Add("Critical Items");
        critical.Cell(1, 1).Value = "Item";
        critical.Cell(1, 2).Value = "Failures";
        row = 2;
        foreach (var item in dashboard.CriticalItems)
        {
            critical.Cell(row, 1).Value = item.ItemText;
            critical.Cell(row, 2).Value = item.FailedCount;
            row++;
        }
        critical.Range(1, 1, 1, 2).Style.Font.Bold = true;
        critical.Columns().AdjustToContents();

        var sections = workbook.Worksheets.Add("Section Averages");
        sections.Cell(1, 1).Value = "Section";
        sections.Cell(1, 2).Value = "Average %";
        row = 2;
        foreach (var item in dashboard.Sections)
        {
            sections.Cell(row, 1).Value = item.SectionName;
            sections.Cell(row, 2).Value = $"{Math.Round(item.AveragePercent, 1)}%";
            row++;
        }
        sections.Range(1, 1, 1, 2).Style.Font.Bold = true;
        sections.Columns().AdjustToContents();

        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"CircleK-Quality-Report-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return new ReportExportResult(stream.ToArray(), fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task<List<SessionSnapshot>> GetSessionSnapshotsAsync(ReportQuery query, CancellationToken cancellationToken)
    {
        var sessionsQuery = _context.AuditSessions
            .AsNoTracking()
            .Include(s => s.Branch)
            .AsQueryable();

        if (query.BrandId.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => s.Branch != null && s.Branch.BrandId == query.BrandId.Value);
        }

        if (query.BranchId.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => s.BranchId == query.BranchId.Value);
        }

        if (query.AuditId.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => s.Id == query.AuditId.Value);
        }

        if (query.From.HasValue)
        {
            var fromDate = query.From.Value.Date;
            sessionsQuery = sessionsQuery.Where(s => s.Date >= fromDate);
        }

        if (query.To.HasValue)
        {
            // Treat "to" as end-of-day so date-only filters include the entire selected day.
            var toExclusive = query.To.Value.Date.AddDays(1);
            sessionsQuery = sessionsQuery.Where(s => s.Date < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            sessionsQuery = sessionsQuery.Where(s => s.Branch != null &&
                                                    (s.Branch.Name.Contains(search) ||
                                                     (s.Branch.NameAr != null && s.Branch.NameAr.Contains(search))));
        }

        return await sessionsQuery
            .Select(s => new SessionSnapshot(
                s.Id,
                s.BranchId,
                s.Branch != null ? s.Branch.Name : string.Empty,
                s.Branch != null ? s.Branch.NameAr : null,
                s.Date,
                s.SubmittedAt,
                s.TotalScore,
                s.TotalMax,
                s.CriticalFailedCount))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<BranchRiskRowDto> BuildBranchRiskRows(IEnumerable<SessionSnapshot> sessions)
    {
        var latestByBranch = sessions
            .GroupBy(s => s.BranchId)
            .Select(g => g
                .OrderByDescending(x => x.SubmittedAt ?? x.Date)
                .First())
            .Select(s => new BranchRiskRowDto(
                s.BranchId,
                s.BranchName,
                s.BranchNameAr,
                s.SubmittedAt ?? s.Date,
                s.TotalScore,
                s.TotalMax,
                s.CriticalFailedCount,
                ComputeRisk(s.TotalScore, s.TotalMax, s.CriticalFailedCount)))
            .OrderByDescending(r => (int)r.RiskLevel)
            .ThenBy(r => r.BranchName)
            .ToList();

        return latestByBranch;
    }

    private async Task<IReadOnlyList<SectionAverageDto>> GetSectionAveragesAsync(IReadOnlyList<Guid> sessionIds, CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0)
        {
            return Array.Empty<SectionAverageDto>();
        }

        var rows = await _context.AuditAnswers
            .AsNoTracking()
            .Where(a => sessionIds.Contains(a.SessionId))
            .Select(a => new
            {
                a.Answer,
                ItemWop = a.Item != null ? a.Item.WOP : 0,
                SectionId = a.Item != null ? a.Item.SectionId : Guid.Empty,
                SectionName = a.Item != null && a.Item.Section != null ? a.Item.Section.Name : string.Empty,
                SectionNameAr = a.Item != null && a.Item.Section != null ? a.Item.Section.NameAr : null
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.SectionId != Guid.Empty)
            .GroupBy(r => new { r.SectionId, r.SectionName, r.SectionNameAr })
            .Select(g =>
            {
                var max = g.Where(x => x.Answer != AnswerType.NA).Sum(x => x.ItemWop);
                var score = g.Where(x => x.Answer == AnswerType.Yes).Sum(x => x.ItemWop);
                var percent = max > 0 ? Math.Round(score / (double)max * 100, 1) : 0;
                return new SectionAverageDto(g.Key.SectionId, g.Key.SectionName, g.Key.SectionNameAr, percent);
            })
            .OrderByDescending(s => s.AveragePercent)
            .ToList();
    }

    private async Task<IReadOnlyList<CriticalItemDto>> GetCriticalItemsAsync(IReadOnlyList<Guid> sessionIds, CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0)
        {
            return Array.Empty<CriticalItemDto>();
        }

        var grouped = await _context.AuditAnswers
            .AsNoTracking()
            .Where(a => sessionIds.Contains(a.SessionId))
            .Where(a => a.Answer == AnswerType.No)
            .Where(a => a.Item != null && a.Item.IsCritical)
            .GroupBy(a => a.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                FailedCount = g.Count()
            })
            .OrderByDescending(c => c.FailedCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (grouped.Count == 0)
        {
            return Array.Empty<CriticalItemDto>();
        }

        var itemIds = grouped.Select(g => g.ItemId).ToList();
        var itemLookup = await _context.AuditItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Text, i.TextAr })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        return grouped
            .Select(g =>
            {
                if (itemLookup.TryGetValue(g.ItemId, out var item))
                {
                    return new CriticalItemDto(item.Id, item.Text, item.TextAr, g.FailedCount);
                }

                return new CriticalItemDto(g.ItemId, string.Empty, null, g.FailedCount);
            })
            .ToList();
    }

    private static RiskLevel ComputeRisk(int totalScore, int totalMax, int criticalFailed)
    {
        if (criticalFailed > 0)
        {
            return RiskLevel.Danger;
        }

        if (totalMax > 0 && totalScore / (double)totalMax >= 0.70)
        {
            return RiskLevel.Safe;
        }

        return RiskLevel.Warning;
    }

    private sealed record SessionSnapshot(
        Guid Id,
        Guid BranchId,
        string BranchName,
        string? BranchNameAr,
        DateTime Date,
        DateTime? SubmittedAt,
        int TotalScore,
        int TotalMax,
        int CriticalFailedCount);
}
