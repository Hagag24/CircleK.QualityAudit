using CircleK.QualityAudit.Application.Reports.Models;

namespace CircleK.QualityAudit.Application.Abstractions.Services;

public interface IReportService
{
    Task<DashboardReportDto> GetDashboardAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchRiskRowDto>> GetBranchScoresAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CriticalItemDto>> GetCriticalItemsAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<ReportExportResult?> ExportAsync(ReportQuery query, CancellationToken cancellationToken = default);
}
