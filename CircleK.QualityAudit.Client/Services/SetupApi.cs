using System.Net.Http.Json;
using CircleK.QualityAudit.Client.Http;
using CircleK.QualityAudit.Client.Dtos;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleK.QualityAudit.Client.Services;

public sealed class SetupApi
{
    private readonly HttpClient _http;
    private readonly CsrfTokenService _csrf;

    public SetupApi(HttpClient http, CsrfTokenService csrf)
    {
        _http = http;
        _csrf = csrf;
    }

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/setup/brands", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<BrandDto>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<BrandDto>>(cancellationToken: cancellationToken)
            ?? Array.Empty<BrandDto>();
    }

    public async Task<BrandDto?> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/setup/brands", request, cancellationToken);
        return await ReadOrDefaultAsync<BrandDto>(response, cancellationToken);
    }

    public async Task<BrandDto?> UpdateBrandAsync(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/setup/brands/{id}", request, cancellationToken);
        return await ReadOrDefaultAsync<BrandDto>(response, cancellationToken);
    }

    public async Task<bool> DeleteBrandAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Delete, $"/api/setup/brands/{id}", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? brandId, CancellationToken cancellationToken = default)
    {
        var url = brandId.HasValue ? $"/api/setup/branches?brandId={brandId}" : "/api/setup/branches";
        var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<BranchDto>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<BranchDto>>(cancellationToken: cancellationToken)
            ?? Array.Empty<BranchDto>();
    }

    public async Task<BranchDto?> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/setup/branches", request, cancellationToken);
        return await ReadOrDefaultAsync<BranchDto>(response, cancellationToken);
    }

    public async Task<BranchDto?> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/setup/branches/{id}", request, cancellationToken);
        return await ReadOrDefaultAsync<BranchDto>(response, cancellationToken);
    }

    public async Task<bool> DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Delete, $"/api/setup/branches/{id}", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<TemplateListDto>> GetTemplatesAsync(Guid? brandId, CancellationToken cancellationToken = default)
    {
        var url = brandId.HasValue ? $"/api/setup/templates?brandId={brandId}" : "/api/setup/templates";
        var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<TemplateListDto>();
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TemplateListDto>>(cancellationToken: cancellationToken)
            ?? Array.Empty<TemplateListDto>();
    }

    public async Task<TemplateDetailsDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/setup/templates/{id}", null, cancellationToken);
        return await ReadOrDefaultAsync<TemplateDetailsDto>(response, cancellationToken);
    }

    public async Task<TemplateListDto?> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/setup/templates", request, cancellationToken);
        return await ReadOrDefaultAsync<TemplateListDto>(response, cancellationToken);
    }

    public async Task<TemplateListDto?> UpdateTemplateAsync(Guid id, UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/setup/templates/{id}", request, cancellationToken);
        return await ReadOrDefaultAsync<TemplateListDto>(response, cancellationToken);
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Delete, $"/api/setup/templates/{id}", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<SectionDto?> AddSectionAsync(Guid templateId, CreateSectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/setup/templates/{templateId}/sections", request, cancellationToken);
        return await ReadOrDefaultAsync<SectionDto>(response, cancellationToken);
    }

    public async Task<SectionDto?> UpdateSectionAsync(Guid id, UpdateSectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/setup/sections/{id}", request, cancellationToken);
        return await ReadOrDefaultAsync<SectionDto>(response, cancellationToken);
    }

    public async Task<bool> DeleteSectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Delete, $"/api/setup/sections/{id}", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReorderSectionsAsync(ReorderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, "/api/setup/sections/reorder", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<ItemDto?> AddItemAsync(Guid sectionId, CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/setup/sections/{sectionId}/items", request, cancellationToken);
        return await ReadOrDefaultAsync<ItemDto>(response, cancellationToken);
    }

    public async Task<ItemDto?> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, $"/api/setup/items/{id}", request, cancellationToken);
        return await ReadOrDefaultAsync<ItemDto>(response, cancellationToken);
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Delete, $"/api/setup/items/{id}", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ReorderItemsAsync(ReorderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, "/api/setup/items/reorder", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<DatabaseBackupDashboardDto?> GetDatabaseBackupDashboardAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/setup/database/backups", null, cancellationToken);
        return await ReadOrDefaultAsync<DatabaseBackupDashboardDto>(response, cancellationToken);
    }

    public async Task<DatabaseBackupRecordDto?> CreateDatabaseBackupAsync(CreateDatabaseBackupRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/setup/database/backups", request, cancellationToken);
        try
        {
            return await response.Content.ReadFromJsonAsync<DatabaseBackupRecordDto>(cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<RestoreDatabaseBackupResult> RestoreDatabaseBackupAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/setup/database/backups/{backupId}/restore", null, cancellationToken);
        RestoreDatabaseBackupResult? payload = null;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<RestoreDatabaseBackupResult>(cancellationToken: cancellationToken);
        }
        catch
        {
            payload = null;
        }
        if (payload is not null)
        {
            return payload;
        }

        var error = response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync(cancellationToken);
        return new RestoreDatabaseBackupResult(response.IsSuccessStatusCode, error);
    }

    public async Task<DatabaseBackupScheduleDto?> UpdateDatabaseBackupScheduleAsync(UpdateDatabaseBackupScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(HttpMethod.Put, "/api/setup/database/schedule", request, cancellationToken);
        return await ReadOrDefaultAsync<DatabaseBackupScheduleDto>(response, cancellationToken);
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

