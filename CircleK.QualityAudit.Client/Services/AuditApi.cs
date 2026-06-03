using System.Net;
using System.Net.Http.Json;
using CircleK.QualityAudit.Client.Dtos;
using CircleK.QualityAudit.Client.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleK.QualityAudit.Client.Services;

public sealed class AuditApi
{
    private readonly HttpClient _http;
    private readonly CsrfTokenService _csrf;

    public AuditApi(HttpClient http, CsrfTokenService csrf)
    {
        _http = http;
        _csrf = csrf;
    }

    public async Task<PaginatedResult<AuditSummaryDto>> GetAuditsAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var url = BuildAuditsUrl(query);
        var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PaginatedResult<AuditSummaryDto>(Array.Empty<AuditSummaryDto>(), query.Page, query.PageSize, 0);
        }

        return await response.Content.ReadFromJsonAsync<PaginatedResult<AuditSummaryDto>>(cancellationToken: cancellationToken)
            ?? new PaginatedResult<AuditSummaryDto>(Array.Empty<AuditSummaryDto>(), query.Page, query.PageSize, 0);
    }

    public async Task<AuditSessionDto?> CreateAuditAsync(CreateAuditRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/audits", request, cancellationToken);
        return await ReadOrDefaultAsync<AuditSessionDto>(response, cancellationToken);
    }

    public async Task<AuditFormDto?> GetAuditFormAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/audits/{sessionId}/form", null, cancellationToken);
        return await ReadOrDefaultAsync<AuditFormDto>(response, cancellationToken);
    }

    public async Task<AuditSessionDetailDto?> GetAuditDetailAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/audits/{sessionId}", null, cancellationToken);
        return await ReadOrDefaultAsync<AuditSessionDetailDto>(response, cancellationToken);
    }

    public async Task<AuditAnswerDto?> SaveAnswerAsync(Guid sessionId, SaveAnswerRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/audits/{sessionId}/answers", request, cancellationToken);
        return await ReadOrDefaultAsync<AuditAnswerDto>(response, cancellationToken);
    }

    public async Task<AuditItemTimingDto?> AddTimingAsync(Guid sessionId, SaveTimingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/audits/{sessionId}/timings", request, cancellationToken);
        return await ReadOrDefaultAsync<AuditItemTimingDto>(response, cancellationToken);
    }

    public async Task<SubmitAuditResultDto> SubmitAuditAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/audits/{sessionId}/submit", null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var session = await response.Content.ReadFromJsonAsync<AuditSessionDto>(cancellationToken: cancellationToken);
            return new SubmitAuditResultDto(session, Array.Empty<Guid>(), null);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var payload = await response.Content.ReadFromJsonAsync<SubmitErrorPayload>(cancellationToken: cancellationToken);
            return new SubmitAuditResultDto(null, payload?.MissingItemIds ?? Array.Empty<Guid>(), payload?.Message ?? "Validation error.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new SubmitAuditResultDto(null, Array.Empty<Guid>(), "You are not allowed to submit this audit.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new SubmitAuditResultDto(null, Array.Empty<Guid>(), "Your session expired. Please sign in again.");
        }

        var serverMessage = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(serverMessage))
        {
            return new SubmitAuditResultDto(null, Array.Empty<Guid>(), serverMessage);
        }

        return new SubmitAuditResultDto(null, Array.Empty<Guid>(), "Request failed.");
    }

    public string BuildAuditExportUrl(Guid sessionId, string format, string? culture, bool autoPrint = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(format))
        {
            parts.Add($"format={Uri.EscapeDataString(format)}");
        }

        if (!string.IsNullOrWhiteSpace(culture))
        {
            parts.Add($"culture={Uri.EscapeDataString(culture)}");
        }

        if (autoPrint)
        {
            parts.Add("autoPrint=true");
        }

        var relativeUrl = parts.Count == 0
            ? $"api/audits/{sessionId}/export"
            : $"api/audits/{sessionId}/export?{string.Join("&", parts)}";

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

    private static string BuildAuditsUrl(AuditQuery query)
    {
        var parts = new List<string>();

        if (query.BranchId.HasValue)
        {
            parts.Add($"branchId={query.BranchId}");
        }

        if (query.BrandId.HasValue)
        {
            parts.Add($"brandId={query.BrandId}");
        }

        if (query.From.HasValue)
        {
            parts.Add($"from={Uri.EscapeDataString(query.From.Value.ToString("yyyy-MM-dd"))}");
        }

        if (query.To.HasValue)
        {
            parts.Add($"to={Uri.EscapeDataString(query.To.Value.ToString("yyyy-MM-dd"))}");
        }

        if (query.Status.HasValue)
        {
            parts.Add($"status={(int)query.Status.Value}");
        }

        if (!string.IsNullOrWhiteSpace(query.InspectorId))
        {
            parts.Add($"inspectorId={Uri.EscapeDataString(query.InspectorId)}");
        }

        parts.Add($"page={query.Page}");
        parts.Add($"pageSize={query.PageSize}");

        return parts.Count > 0 ? $"/api/audits?{string.Join("&", parts)}" : "/api/audits";
    }

    private sealed class SubmitErrorPayload
    {
        public string? Message { get; set; }
        public IReadOnlyList<Guid>? MissingItemIds { get; set; }
    }
}
