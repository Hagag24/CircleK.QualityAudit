using System.Net.Http.Json;
using CircleK.QualityAudit.Client.Dtos;
using CircleK.QualityAudit.Client.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleK.QualityAudit.Client.Services;

public sealed class UsersApi
{
    private readonly HttpClient _http;
    private readonly CsrfTokenService _csrf;

    public UsersApi(HttpClient http, CsrfTokenService csrf)
    {
        _http = http;
        _csrf = csrf;
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/users", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<UserListItemDto>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserListItemDto>>(cancellationToken: cancellationToken)
            ?? Array.Empty<UserListItemDto>();
    }

    public async Task<UserDetailsDto?> GetUserAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/users/{id}", null, cancellationToken);
        return await ReadOrDefaultAsync<UserDetailsDto>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/users/roles", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>(cancellationToken: cancellationToken)
            ?? Array.Empty<string>();
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/users/permissions", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>(cancellationToken: cancellationToken)
            ?? Array.Empty<string>();
    }

    public async Task<UserDetailsDto?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/users", request, cancellationToken);
        return await ReadOrDefaultAsync<UserDetailsDto>(response, cancellationToken);
    }

    public async Task<UserDetailsDto?> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/users/{id}", request, cancellationToken);
        return await ReadOrDefaultAsync<UserDetailsDto>(response, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string id, ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/users/{id}/reset-password", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<string>> UpdatePermissionsAsync(string id, UpdatePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/users/{id}/permissions", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>(cancellationToken: cancellationToken)
            ?? Array.Empty<string>();
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
}
