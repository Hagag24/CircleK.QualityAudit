using System.Net.Http.Json;
using CircleK.QualityAudit.Client.Dtos;
using CircleK.QualityAudit.Client.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleK.QualityAudit.Client.Services;

public sealed class ReportApi
{
    private readonly HttpClient _http;
    private readonly CsrfTokenService _csrf;

    public ReportApi(HttpClient http, CsrfTokenService csrf)
    {
        _http = http;
        _csrf = csrf;
    }

    public async Task<DashboardReportDto?> GetDashboardAsync(ReportQuery query, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl("api/reports/dashboard", query);
        var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        return await ReadOrDefaultAsync<DashboardReportDto>(response, cancellationToken);
    }

    public string BuildExportUrl(ReportQuery query)
        => BuildUrl("api/reports/export", query);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options && method != HttpMethod.Trace)
        {
            await _csrf.EnsureTokenAsync(cancellationToken);
        }

        return await _http.SendAsync(request, cancellationToken);
    }

    private static async Task<T?> ReadOrDefaultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private string BuildUrl(string baseUrl, ReportQuery query)
    {
        var parts = new List<string>();

        if (query.BrandId.HasValue)
        {
            parts.Add($"brandId={query.BrandId}");
        }

        if (query.BranchId.HasValue)
        {
            parts.Add($"branchId={query.BranchId}");
        }

        if (query.AuditId.HasValue)
        {
            parts.Add($"auditId={query.AuditId}");
        }

        if (query.From.HasValue)
        {
            parts.Add($"from={Uri.EscapeDataString(query.From.Value.ToString("yyyy-MM-dd"))}");
        }

        if (query.To.HasValue)
        {
            parts.Add($"to={Uri.EscapeDataString(query.To.Value.ToString("yyyy-MM-dd"))}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"search={Uri.EscapeDataString(query.Search)}");
        }

        var relativeUrl = parts.Count > 0 ? $"{baseUrl}?{string.Join("&", parts)}" : baseUrl;
        return new Uri(GetApiBaseAddress(), relativeUrl).ToString();
    }

    private Uri GetApiBaseAddress()
    {
        if (_http.BaseAddress is null)
        {
            throw new InvalidOperationException("API base address is not configured.");
        }

        var absolute = _http.BaseAddress.AbsoluteUri;
        if (!absolute.EndsWith("/", StringComparison.Ordinal))
        {
            absolute += "/";
        }

        return new Uri(absolute, UriKind.Absolute);
    }
}
