using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleK.QualityAudit.Client.Http;

public sealed class CsrfTokenService
{
    private readonly HttpClient _http;
    private readonly CsrfTokenStore _store;

    public CsrfTokenService(HttpClient http, CsrfTokenStore store)
    {
        _http = http;
        _store = store;
    }

    public async Task EnsureTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_store.Token))
        {
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>(cancellationToken: cancellationToken);
        if (payload?.Token is not null)
        {
            _store.Token = payload.Token;
        }
    }

    private sealed record CsrfTokenResponse(string Token);
}
