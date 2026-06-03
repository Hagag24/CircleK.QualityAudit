using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleK.QualityAudit.Client.Auth;

public sealed class CookieAuthStateProvider : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly HttpClient _http;

    public CookieAuthStateProvider(HttpClient http)
    {
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            var response = await _http.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new AuthenticationState(Anonymous);
            }

            response.EnsureSuccessStatusCode();
            var user = await response.Content.ReadFromJsonAsync<UserInfoDto>();
            if (user is null)
            {
                return new AuthenticationState(Anonymous);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.Permissions?.Count > 0)
            {
                claims.AddRange(user.Permissions.Select(p => new Claim("permission", p)));
            }

            var identity = new ClaimsIdentity(claims, "CookieAuth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public void NotifyUserAuthentication()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }
}
