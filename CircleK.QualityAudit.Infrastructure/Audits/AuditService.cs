using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.Audits.Models;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Domain.Enums;
using CircleK.QualityAudit.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

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
        var branch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BranchId && b.IsActive, cancellationToken);

        if (branch is null)
        {
            return null;
        }

        var template = await _context.AuditTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.IsActive, cancellationToken);

        if (template is null)
        {
            return null;
        }

        var inspectorName = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == inspectorId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

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
        await _context.SaveChangesAsync(cancellationToken);

        return MapSessionDto(session, branch.Name, branch.NameAr, template.Name, inspectorName);
    }

    public async Task<AuditFormDto?> GetAuditFormAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.AuditSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        return await _context.AuditTemplates
            .AsNoTracking()
            .Where(t => t.Id == session.TemplateId)
            .Select(t => new AuditFormDto(
                sessionId,
                t.Name,
                t.Sections
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
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AuditAnswerDto?> SaveAnswerAsync(Guid sessionId, SaveAnswerRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _context.AuditSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || session.Status == AuditStatus.Submitted)
        {
            return null;
        }

        var item = await _context.AuditItems
            .Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item is null || item.Section is null || item.Section.TemplateId != session.TemplateId)
        {
            return null;
        }

        var answer = await _context.AuditAnswers
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.ItemId == request.ItemId, cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

        return new AuditAnswerDto(answer.Id, answer.SessionId, answer.ItemId, answer.Answer, answer.Score, answer.Notes);
    }

    public async Task<AuditItemTimingDto?> AddTimingAsync(Guid sessionId, SaveTimingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DurationSeconds <= 0)
        {
            return null;
        }

        var session = await _context.AuditSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || session.Status == AuditStatus.Submitted)
        {
            return null;
        }

        var item = await _context.AuditItems
            .Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

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
        await _context.SaveChangesAsync(cancellationToken);

        var entries = await _context.AuditTimingMeasurements
            .AsNoTracking()
            .Where(t => t.SessionId == sessionId && t.ItemId == request.ItemId)
            .OrderBy(t => t.RecordedAt)
            .Select(t => new AuditTimingEntryDto(t.Id, t.DurationSeconds, t.RecordedAt))
            .ToListAsync(cancellationToken);

        return MapItemTiming(request.ItemId, entries);
    }

    public async Task<SubmitAuditResult?> SubmitAuditAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.AuditSessions
            .Include(s => s.Branch)
            .Include(s => s.Template)
            .Include(s => s.Inspector)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

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
            .FirstOrDefaultAsync(t => t.Id == session.TemplateId, cancellationToken);

        if (template is null)
        {
            return null;
        }

        var answers = await _context.AuditAnswers
            .Where(a => a.SessionId == sessionId)
            .ToListAsync(cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

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
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AuditSessionDetailDto?> GetAuditDetailAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.AuditSessions
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.Template)
            .Include(s => s.Inspector)
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

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
            .ToListAsync(cancellationToken);

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

        var totalCount = await auditsQuery.CountAsync(cancellationToken);

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
            .ToListAsync(cancellationToken);

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
        var report = BuildExportReport(detail, form, isArabic);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileBase = $"CircleK-Audit-{detail.Session.Id:N}-{stamp}";

        return format switch
        {
            AuditExportFormat.Excel => BuildExcelExport(report, fileBase),
            AuditExportFormat.Csv => BuildCsvExport(report, fileBase),
            _ => BuildHtmlExport(report, fileBase, autoPrint)
        };
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

    private static AuditItemTimingDto MapItemTiming(Guid itemId, IReadOnlyList<AuditTimingEntryDto> entries)
    {
        var count = entries.Count;
        var average = count > 0 ? entries.Average(e => e.DurationSeconds) : 0;

        return new AuditItemTimingDto(itemId, count, average, entries);
    }

    private static AuditExportReport BuildExportReport(AuditSessionDetailDto detail, AuditFormDto form, bool isArabic)
    {
        var answerLookup = detail.Answers.ToDictionary(a => a.ItemId, a => a);
        var timingLookup = detail.Timings.ToDictionary(t => t.ItemId, t => t);

        var sections = new List<AuditExportSection>();
        var totalItems = 0;
        var answeredItems = 0;
        var totalScore = 0;
        var totalMax = 0;
        var criticalFailed = 0;

        foreach (var section in form.Sections.OrderBy(s => s.Order))
        {
            var sectionRows = new List<AuditExportItemRow>();
            var sectionScore = 0;
            var sectionMax = 0;
            var sectionAnswered = 0;

            foreach (var item in section.Items.OrderBy(i => i.Order))
            {
                totalItems++;

                answerLookup.TryGetValue(item.Id, out var answer);
                timingLookup.TryGetValue(item.Id, out var timing);

                if (answer is not null)
                {
                    answeredItems++;
                    sectionAnswered++;
                }

                var score = answer?.Score ?? 0;
                var max = answer is not null && answer.Answer != AnswerType.NA ? item.Wop : 0;

                sectionScore += score;
                sectionMax += max;

                totalScore += score;
                totalMax += max;

                if (item.IsCritical && answer?.Answer == AnswerType.No)
                {
                    criticalFailed++;
                }

                sectionRows.Add(new AuditExportItemRow(
                    item.Id,
                    isArabic && !string.IsNullOrWhiteSpace(item.TextAr) ? item.TextAr! : item.Text,
                    item.Wop,
                    item.IsCritical,
                    answer?.Answer,
                    answer is null ? (isArabic ? "\u063A\u064A\u0631 \u0645\u062C\u0627\u0628" : "Unanswered") : GetAnswerText(answer.Answer, isArabic),
                    score,
                    max,
                    answer?.Notes,
                    timing?.Count ?? 0,
                    timing?.AverageSeconds ?? 0,
                    timing?.Entries ?? Array.Empty<AuditTimingEntryDto>()));
            }

            var sectionName = isArabic && !string.IsNullOrWhiteSpace(section.NameAr) ? section.NameAr! : section.Name;
            sections.Add(new AuditExportSection(sectionName, sectionAnswered, section.Items.Count, sectionScore, sectionMax, sectionRows));
        }

        return new AuditExportReport(
            detail.Session,
            form.TemplateName,
            sections,
            totalItems,
            answeredItems,
            totalScore,
            totalMax,
            criticalFailed,
            ComputeRisk(totalScore, totalMax, criticalFailed),
            isArabic);
    }

    private static AuditExportResult BuildHtmlExport(AuditExportReport report, string fileBase, bool autoPrint)
    {
        var sb = new StringBuilder(32_768);
        var dir = report.IsArabic ? "rtl" : "ltr";
        var title = report.IsArabic ? "\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u062A\u062F\u0642\u064A\u0642" : "Audit Report";
        var riskText = GetRiskText(report.RiskLevel, report.IsArabic);
        var percent = report.TotalMax > 0 ? Math.Round(report.TotalScore / (double)report.TotalMax * 100) : 0;

        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"" + (report.IsArabic ? "ar" : "en") + "\" dir=\"" + dir + "\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("<title>" + WebUtility.HtmlEncode(title) + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Cairo','Segoe UI',Tahoma,sans-serif;background:#f5f6f8;color:#111;margin:0;}");
        sb.AppendLine(".wrap{max-width:1180px;margin:0 auto;padding:28px 20px 40px;}");
        sb.AppendLine(".hero{background:linear-gradient(130deg,#0f1115,#1a1f27);color:#fff;padding:22px 24px;border-radius:18px;box-shadow:0 12px 35px rgba(0,0,0,.16);}");
        sb.AppendLine(".brand{display:flex;align-items:center;gap:12px;}");
        sb.AppendLine(".logo{width:46px;height:46px;border-radius:50%;background:#e10600;display:flex;align-items:center;justify-content:center;font-weight:800;font-size:24px;}");
        sb.AppendLine(".muted{color:#c9d0db;font-size:13px;}");
        sb.AppendLine(".meta{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin-top:16px;}");
        sb.AppendLine(".meta .box{background:rgba(255,255,255,.08);padding:10px 12px;border-radius:10px;}");
        sb.AppendLine(".meta .k{font-size:12px;color:#d9dee6;}");
        sb.AppendLine(".meta .v{font-weight:700;font-size:14px;margin-top:3px;}");
        sb.AppendLine(".kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-top:14px;}");
        sb.AppendLine(".kpi{background:#fff;border:1px solid #eceff3;border-radius:14px;padding:14px;}");
        sb.AppendLine(".kpi .k{font-size:12px;color:#6f7785;}");
        sb.AppendLine(".kpi .v{font-size:26px;font-weight:800;line-height:1.2;margin-top:4px;}");
        sb.AppendLine(".section{margin-top:14px;background:#fff;border:1px solid #eceff3;border-radius:14px;overflow:hidden;}");
        sb.AppendLine(".section-head{display:flex;justify-content:space-between;align-items:center;padding:14px 16px;background:#f9fafb;border-bottom:1px solid #edf0f4;}");
        sb.AppendLine(".pill{background:#f1f4f8;border-radius:999px;padding:5px 10px;font-size:12px;font-weight:700;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;}");
        sb.AppendLine("th,td{padding:10px 12px;border-bottom:1px solid #f0f2f6;vertical-align:top;font-size:13px;}");
        sb.AppendLine("th{background:#fff7f7;color:#a20e0e;font-weight:800;position:sticky;top:0;}");
        sb.AppendLine("tbody tr:nth-child(even){background:#fcfdff;}");
        sb.AppendLine(".badge{padding:3px 8px;border-radius:999px;font-size:11px;font-weight:700;display:inline-block;}");
        sb.AppendLine(".badge.safe{background:#e8f7ef;color:#0f8f4a;}.badge.warn{background:#fff3cd;color:#9a6b00;}.badge.danger{background:#fde9ec;color:#be1e2d;}");
        sb.AppendLine(".answer{font-weight:700;}.critical{color:#be1e2d;font-weight:700;}");
        sb.AppendLine(".time{color:#404857;font-size:12px;}");
        sb.AppendLine(".foot{margin-top:16px;font-size:12px;color:#6d7482;}");
        sb.AppendLine("@media (max-width:960px){.meta,.kpis{grid-template-columns:repeat(2,minmax(0,1fr));}}");
        sb.AppendLine("@media print{body{background:#fff}.wrap{padding:0}.section{break-inside:avoid}.hero{box-shadow:none}.no-print{display:none !important}}");
        sb.AppendLine("</style>");
        if (autoPrint)
        {
            sb.AppendLine("<script>window.addEventListener('load',()=>window.print());</script>");
        }
        sb.AppendLine("</head><body><div class=\"wrap\">");
        sb.AppendLine("<div class=\"hero\">");
        sb.AppendLine("<div class=\"brand\"><div class=\"logo\">K</div><div><div style=\"font-size:22px;font-weight:800;\">Circle K</div><div class=\"muted\">"
            + WebUtility.HtmlEncode(title) + "</div></div></div>");
        sb.AppendLine("<div class=\"meta\">");
        AppendMeta(sb, report.IsArabic ? "\u0627\u0644\u0641\u0631\u0639" : "Branch", GetBranchName(report.Session, report.IsArabic));
        AppendMeta(sb, report.IsArabic ? "\u0627\u0644\u0642\u0627\u0644\u0628" : "Template", report.TemplateName);
        AppendMeta(sb, report.IsArabic ? "\u0627\u0644\u0645\u062F\u0642\u0642" : "Inspector", report.Session.InspectorName);
        AppendMeta(sb, report.IsArabic ? "\u0627\u0644\u062A\u0627\u0631\u064A\u062E" : "Date", report.Session.Date.ToString("yyyy-MM-dd"));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class=\"kpis\">");
        AppendKpi(sb, report.IsArabic ? "\u0627\u0644\u0646\u062A\u064A\u062C\u0629" : "Score", $"{report.TotalScore} / {report.TotalMax}");
        AppendKpi(sb, report.IsArabic ? "\u0627\u0644\u0646\u0633\u0628\u0629" : "Percent", $"{percent}%");
        AppendKpi(sb, report.IsArabic ? "\u062D\u0631\u062C \u0641\u0627\u0634\u0644" : "Critical Failed", report.CriticalFailedCount.ToString());
        AppendKpi(sb, report.IsArabic ? "\u0627\u0644\u0645\u062E\u0627\u0637\u0631\u0629" : "Risk", riskText);
        sb.AppendLine("</div>");

        foreach (var section in report.Sections)
        {
            sb.AppendLine("<section class=\"section\">");
            sb.AppendLine("<div class=\"section-head\"><div style=\"font-weight:800;\">"
                + WebUtility.HtmlEncode(section.Name)
                + "</div><div class=\"pill\">"
                + WebUtility.HtmlEncode((report.IsArabic ? "\u0627\u0644\u0646\u0642\u0627\u0637: " : "Score: ") + $"{section.Score}/{section.Max}")
                + "</div></div>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>" + WebUtility.HtmlEncode(report.IsArabic ? "\u0627\u0644\u0628\u0646\u062F" : "Item") + "</th>");
            sb.AppendLine("<th>" + WebUtility.HtmlEncode("WOP") + "</th>");
            sb.AppendLine("<th>" + WebUtility.HtmlEncode(report.IsArabic ? "\u0627\u0644\u0625\u062C\u0627\u0628\u0629" : "Answer") + "</th>");
            sb.AppendLine("<th>" + WebUtility.HtmlEncode(report.IsArabic ? "\u0627\u0644\u0646\u0642\u0627\u0637" : "Score") + "</th>");
            sb.AppendLine("<th>" + WebUtility.HtmlEncode(report.IsArabic ? "\u0627\u0644\u062A\u0648\u0642\u064A\u062A" : "Timing") + "</th>");
            sb.AppendLine("<th>" + WebUtility.HtmlEncode(report.IsArabic ? "\u0645\u0644\u0627\u062D\u0638\u0627\u062A" : "Notes") + "</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var row in section.Rows)
            {
                var note = string.IsNullOrWhiteSpace(row.Notes) ? "-" : row.Notes!;
                var timingText = row.TimingCount <= 0
                    ? (report.IsArabic ? "\u0644\u0627 \u064A\u0648\u062C\u062F" : "None")
                    : (report.IsArabic ? $"\u0642\u064A\u0627\u0633\u0627\u062A: {row.TimingCount} | \u0645\u062A\u0648\u0633\u0637: {FormatDuration(row.TimingAverageSeconds)}"
                        : $"Measurements: {row.TimingCount} | Avg: {FormatDuration(row.TimingAverageSeconds)}");
                sb.AppendLine("<tr>");
                sb.AppendLine("<td><div class=\"answer\">"
                    + WebUtility.HtmlEncode(row.Text)
                    + "</div>"
                    + (row.IsCritical ? "<div class=\"critical\">" + WebUtility.HtmlEncode(report.IsArabic ? "\u062D\u0631\u062C" : "Critical") + "</div>" : string.Empty)
                    + "</td>");
                sb.AppendLine("<td>" + row.Wop + "</td>");
                sb.AppendLine("<td>" + WebUtility.HtmlEncode(row.AnswerText) + "</td>");
                sb.AppendLine("<td>" + row.Score + " / " + row.Max + "</td>");
                sb.AppendLine("<td class=\"time\">" + WebUtility.HtmlEncode(timingText) + "</td>");
                sb.AppendLine("<td>" + WebUtility.HtmlEncode(note) + "</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></section>");
        }

        sb.AppendLine("<div class=\"foot\">");
        sb.AppendLine(WebUtility.HtmlEncode((report.IsArabic ? "\u062A\u0645 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062A\u0642\u0631\u064A\u0631: " : "Generated at: ") + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")));
        sb.AppendLine("</div>");
        sb.AppendLine("</div></body></html>");

        return new AuditExportResult(
            Encoding.UTF8.GetBytes(sb.ToString()),
            fileBase + ".html",
            "text/html; charset=utf-8");
    }

    private static AuditExportResult BuildExcelExport(AuditExportReport report, string fileBase)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Summary");
        var percent = report.TotalMax > 0 ? Math.Round(report.TotalScore / (double)report.TotalMax * 100) : 0;

        summary.Cell(1, 1).Value = report.IsArabic ? "\u0627\u0644\u0639\u0646\u0635\u0631" : "Metric";
        summary.Cell(1, 2).Value = report.IsArabic ? "\u0627\u0644\u0642\u064A\u0645\u0629" : "Value";
        summary.Cell(2, 1).Value = report.IsArabic ? "\u0627\u0644\u0641\u0631\u0639" : "Branch";
        summary.Cell(2, 2).Value = GetBranchName(report.Session, report.IsArabic);
        summary.Cell(3, 1).Value = report.IsArabic ? "\u0627\u0644\u0642\u0627\u0644\u0628" : "Template";
        summary.Cell(3, 2).Value = report.TemplateName;
        summary.Cell(4, 1).Value = report.IsArabic ? "\u0627\u0644\u0645\u062F\u0642\u0642" : "Inspector";
        summary.Cell(4, 2).Value = report.Session.InspectorName;
        summary.Cell(5, 1).Value = report.IsArabic ? "\u0627\u0644\u062A\u0627\u0631\u064A\u062E" : "Date";
        summary.Cell(5, 2).Value = report.Session.Date.ToString("yyyy-MM-dd");
        summary.Cell(6, 1).Value = report.IsArabic ? "\u0627\u0644\u0646\u062A\u064A\u062C\u0629" : "Score";
        summary.Cell(6, 2).Value = $"{report.TotalScore}/{report.TotalMax}";
        summary.Cell(7, 1).Value = report.IsArabic ? "\u0627\u0644\u0646\u0633\u0628\u0629" : "Percent";
        summary.Cell(7, 2).Value = $"{percent}%";
        summary.Cell(8, 1).Value = report.IsArabic ? "\u062D\u0631\u062C \u0641\u0627\u0634\u0644" : "Critical Failed";
        summary.Cell(8, 2).Value = report.CriticalFailedCount;
        summary.Cell(9, 1).Value = report.IsArabic ? "\u0627\u0644\u0645\u062E\u0627\u0637\u0631\u0629" : "Risk";
        summary.Cell(9, 2).Value = GetRiskText(report.RiskLevel, report.IsArabic);
        summary.Cell(10, 1).Value = report.IsArabic ? "\u062A\u0645 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062A\u0642\u0631\u064A\u0631" : "Generated at";
        summary.Cell(10, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
        summary.Range(1, 1, 1, 2).Style.Font.Bold = true;
        summary.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FCEBEC");
        summary.Columns().AdjustToContents();

        var details = workbook.Worksheets.Add("Items");
        details.Cell(1, 1).Value = report.IsArabic ? "\u0627\u0644\u0642\u0633\u0645" : "Section";
        details.Cell(1, 2).Value = report.IsArabic ? "\u0627\u0644\u0646\u0642\u0627\u0637/\u0627\u0644\u0642\u0633\u0645" : "Section Score";
        details.Cell(1, 3).Value = report.IsArabic ? "\u0627\u0644\u0628\u0646\u062F" : "Item";
        details.Cell(1, 4).Value = "WOP";
        details.Cell(1, 5).Value = report.IsArabic ? "\u0627\u0644\u0625\u062C\u0627\u0628\u0629" : "Answer";
        details.Cell(1, 6).Value = report.IsArabic ? "\u0627\u0644\u0646\u0642\u0627\u0637" : "Score";
        details.Cell(1, 7).Value = report.IsArabic ? "\u0627\u0644\u062A\u0648\u0642\u064A\u062A" : "Timing";
        details.Cell(1, 8).Value = report.IsArabic ? "\u0645\u0644\u0627\u062D\u0638\u0627\u062A" : "Notes";
        details.Range(1, 1, 1, 8).Style.Font.Bold = true;
        details.Range(1, 1, 1, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF0F1");

        var row = 2;
        foreach (var section in report.Sections)
        {
            foreach (var item in section.Rows)
            {
                var timingText = item.TimingCount <= 0
                    ? (report.IsArabic ? "\u0644\u0627 \u064A\u0648\u062C\u062F" : "None")
                    : (report.IsArabic
                        ? $"\u0642\u064A\u0627\u0633\u0627\u062A: {item.TimingCount} | \u0645\u062A\u0648\u0633\u0637: {FormatDuration(item.TimingAverageSeconds)}"
                        : $"Measurements: {item.TimingCount} | Avg: {FormatDuration(item.TimingAverageSeconds)}");

                var itemText = item.IsCritical
                    ? (report.IsArabic ? $"{item.Text} (\u062D\u0631\u062C)" : $"{item.Text} (Critical)")
                    : item.Text;

                details.Cell(row, 1).Value = section.Name;
                details.Cell(row, 2).Value = $"{section.Score}/{section.Max}";
                details.Cell(row, 3).Value = itemText;
                details.Cell(row, 4).Value = item.Wop;
                details.Cell(row, 5).Value = item.AnswerText;
                details.Cell(row, 6).Value = $"{item.Score} / {item.Max}";
                details.Cell(row, 7).Value = timingText;
                details.Cell(row, 8).Value = string.IsNullOrWhiteSpace(item.Notes) ? "-" : item.Notes!;
                row++;
            }
        }

        details.Columns().AdjustToContents();
        details.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new AuditExportResult(
            stream.ToArray(),
            fileBase + ".xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static AuditExportResult BuildCsvExport(AuditExportReport report, string fileBase)
    {
        var sb = new StringBuilder(32_768);
        var percent = report.TotalMax > 0 ? Math.Round(report.TotalScore / (double)report.TotalMax * 100) : 0;

        var metricHeader = report.IsArabic ? "العنصر" : "Metric";
        var valueHeader = report.IsArabic ? "القيمة" : "Value";
        var branchLabel = report.IsArabic ? "الفرع" : "Branch";
        var templateLabel = report.IsArabic ? "القالب" : "Template";
        var inspectorLabel = report.IsArabic ? "المدقق" : "Inspector";
        var dateLabel = report.IsArabic ? "التاريخ" : "Date";
        var scoreLabel = report.IsArabic ? "النتيجة" : "Score";
        var percentLabel = report.IsArabic ? "النسبة" : "Percent";
        var criticalLabel = report.IsArabic ? "حرج فاشل" : "Critical Failed";
        var riskLabel = report.IsArabic ? "المخاطرة" : "Risk";
        var generatedAtLabel = report.IsArabic ? "تم إنشاء التقرير" : "Generated at";

        sb.AppendLine($"{EscapeCsv(metricHeader)},{EscapeCsv(valueHeader)}");
        sb.AppendLine($"{EscapeCsv(branchLabel)},{EscapeCsv(GetBranchName(report.Session, report.IsArabic))}");
        sb.AppendLine($"{EscapeCsv(templateLabel)},{EscapeCsv(report.TemplateName)}");
        sb.AppendLine($"{EscapeCsv(inspectorLabel)},{EscapeCsv(report.Session.InspectorName)}");
        sb.AppendLine($"{EscapeCsv(dateLabel)},{EscapeCsv(report.Session.Date.ToString("yyyy-MM-dd"))}");
        sb.AppendLine($"{EscapeCsv(scoreLabel)},{EscapeCsv($"{report.TotalScore}/{report.TotalMax}")}");
        sb.AppendLine($"{EscapeCsv(percentLabel)},{EscapeCsv($"{percent}%")}");
        sb.AppendLine($"{EscapeCsv(criticalLabel)},{EscapeCsv(report.CriticalFailedCount.ToString())}");
        sb.AppendLine($"{EscapeCsv(riskLabel)},{EscapeCsv(GetRiskText(report.RiskLevel, report.IsArabic))}");
        sb.AppendLine($"{EscapeCsv(generatedAtLabel)},{EscapeCsv(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"))}");
        sb.AppendLine();

        var sectionHeader = report.IsArabic ? "القسم" : "Section";
        var sectionScoreHeader = report.IsArabic ? "النقاط/القسم" : "Section Score";
        var itemHeader = report.IsArabic ? "البند" : "Item";
        var answerHeader = report.IsArabic ? "الإجابة" : "Answer";
        var itemScoreHeader = report.IsArabic ? "النقاط" : "Score";
        var timingHeader = report.IsArabic ? "التوقيت" : "Timing";
        var notesHeader = report.IsArabic ? "ملاحظات" : "Notes";

        sb.AppendLine(string.Join(",",
            EscapeCsv(sectionHeader),
            EscapeCsv(sectionScoreHeader),
            EscapeCsv(itemHeader),
            EscapeCsv("WOP"),
            EscapeCsv(answerHeader),
            EscapeCsv(itemScoreHeader),
            EscapeCsv(timingHeader),
            EscapeCsv(notesHeader)));

        foreach (var section in report.Sections)
        {
            foreach (var item in section.Rows)
            {
                var timingValue = item.TimingCount <= 0
                    ? (report.IsArabic ? "لا يوجد" : "None")
                    : (report.IsArabic
                        ? $"قياسات: {item.TimingCount} | متوسط: {FormatDuration(item.TimingAverageSeconds)}"
                        : $"Measurements: {item.TimingCount} | Avg: {FormatDuration(item.TimingAverageSeconds)}");

                var itemText = item.IsCritical
                    ? (report.IsArabic ? $"{item.Text} (حرج)" : $"{item.Text} (Critical)")
                    : item.Text;

                sb.AppendLine(string.Join(",",
                    EscapeCsv(section.Name),
                    EscapeCsv($"{section.Score}/{section.Max}"),
                    EscapeCsv(itemText),
                    EscapeCsv(item.Wop.ToString()),
                    EscapeCsv(item.AnswerText),
                    EscapeCsv($"{item.Score} / {item.Max}"),
                    EscapeCsv(timingValue),
                    EscapeCsv(string.IsNullOrWhiteSpace(item.Notes) ? "-" : item.Notes!)));
            }
        }

        var csvBody = Encoding.UTF8.GetBytes(sb.ToString());
        var bom = Encoding.UTF8.GetPreamble();
        var payload = new byte[bom.Length + csvBody.Length];
        Buffer.BlockCopy(bom, 0, payload, 0, bom.Length);
        Buffer.BlockCopy(csvBody, 0, payload, bom.Length, csvBody.Length);

        return new AuditExportResult(
            payload,
            fileBase + ".csv",
            "text/csv; charset=utf-8");
    }

    private static string GetBranchName(AuditSessionDto session, bool isArabic)
        => isArabic && !string.IsNullOrWhiteSpace(session.BranchNameAr) ? session.BranchNameAr! : session.BranchName;

    private static string GetAnswerText(AnswerType answer, bool isArabic)
    {
        if (isArabic)
        {
            return answer switch
            {
                AnswerType.Yes => "\u0646\u0639\u0645",
                AnswerType.No => "\u0644\u0627",
                AnswerType.NA => "\u063A\u064A\u0631 \u0645\u0637\u0628\u0642",
                _ => "\u063A\u064A\u0631 \u0645\u0637\u0628\u0642"
            };
        }

        return answer switch
        {
            AnswerType.Yes => "Yes",
            AnswerType.No => "No",
            AnswerType.NA => "N/A",
            _ => "N/A"
        };
    }

    private static string GetRiskText(RiskLevel level, bool isArabic)
    {
        if (isArabic)
        {
            return level switch
            {
                RiskLevel.Safe => "\u0622\u0645\u0646",
                RiskLevel.Warning => "\u062A\u062D\u0630\u064A\u0631",
                _ => "\u062E\u0637\u0631"
            };
        }

        return level switch
        {
            RiskLevel.Safe => "Safe",
            RiskLevel.Warning => "Warning",
            _ => "Danger"
        };
    }

    private static string FormatDuration(double seconds)
    {
        var rounded = Math.Max(0, (int)Math.Round(seconds));
        var value = TimeSpan.FromSeconds(rounded);
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static void AppendMeta(StringBuilder sb, string label, string value)
    {
        sb.AppendLine("<div class=\"box\"><div class=\"k\">"
            + WebUtility.HtmlEncode(label)
            + "</div><div class=\"v\">"
            + WebUtility.HtmlEncode(value)
            + "</div></div>");
    }

    private static void AppendKpi(StringBuilder sb, string label, string value)
    {
        sb.AppendLine("<div class=\"kpi\"><div class=\"k\">"
            + WebUtility.HtmlEncode(label)
            + "</div><div class=\"v\">"
            + WebUtility.HtmlEncode(value)
            + "</div></div>");
    }

    private sealed record AuditExportReport(
        AuditSessionDto Session,
        string TemplateName,
        IReadOnlyList<AuditExportSection> Sections,
        int TotalItems,
        int AnsweredItems,
        int TotalScore,
        int TotalMax,
        int CriticalFailedCount,
        RiskLevel RiskLevel,
        bool IsArabic);

    private sealed record AuditExportSection(
        string Name,
        int AnsweredItems,
        int TotalItems,
        int Score,
        int Max,
        IReadOnlyList<AuditExportItemRow> Rows);

    private sealed record AuditExportItemRow(
        Guid ItemId,
        string Text,
        int Wop,
        bool IsCritical,
        AnswerType? Answer,
        string AnswerText,
        int Score,
        int Max,
        string? Notes,
        int TimingCount,
        double TimingAverageSeconds,
        IReadOnlyList<AuditTimingEntryDto> TimingEntries);
}

