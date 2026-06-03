using CircleK.QualityAudit.Client;
using CircleK.QualityAudit.Client.Auth;
using CircleK.QualityAudit.Client.Http;
using BlazorBootstrap;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddBlazorBootstrap();

builder.Services.AddScoped<CsrfTokenStore>();
builder.Services.AddTransient<CsrfTokenHandler>();
builder.Services.AddScoped<CsrfTokenService>();
builder.Services.AddScoped<CircleK.QualityAudit.Client.Services.SetupApi>();
builder.Services.AddScoped<CircleK.QualityAudit.Client.Services.AuditApi>();
builder.Services.AddScoped<CircleK.QualityAudit.Client.Services.ReportApi>();
builder.Services.AddScoped<CircleK.QualityAudit.Client.Services.UsersApi>();

var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy(PermissionConstants.BrandView, policy => policy.RequireClaim("permission", PermissionConstants.BrandView));
    options.AddPolicy(PermissionConstants.BrandCreate, policy => policy.RequireClaim("permission", PermissionConstants.BrandCreate));
    options.AddPolicy(PermissionConstants.BrandUpdate, policy => policy.RequireClaim("permission", PermissionConstants.BrandUpdate));
    options.AddPolicy(PermissionConstants.BrandDelete, policy => policy.RequireClaim("permission", PermissionConstants.BrandDelete));
    options.AddPolicy(PermissionConstants.BranchView, policy => policy.RequireClaim("permission", PermissionConstants.BranchView));
    options.AddPolicy(PermissionConstants.BranchCreate, policy => policy.RequireClaim("permission", PermissionConstants.BranchCreate));
    options.AddPolicy(PermissionConstants.BranchUpdate, policy => policy.RequireClaim("permission", PermissionConstants.BranchUpdate));
    options.AddPolicy(PermissionConstants.BranchDelete, policy => policy.RequireClaim("permission", PermissionConstants.BranchDelete));
    options.AddPolicy(PermissionConstants.TemplateView, policy => policy.RequireClaim("permission", PermissionConstants.TemplateView));
    options.AddPolicy(PermissionConstants.TemplateCreate, policy => policy.RequireClaim("permission", PermissionConstants.TemplateCreate));
    options.AddPolicy(PermissionConstants.TemplateUpdate, policy => policy.RequireClaim("permission", PermissionConstants.TemplateUpdate));
    options.AddPolicy(PermissionConstants.TemplateDelete, policy => policy.RequireClaim("permission", PermissionConstants.TemplateDelete));
    options.AddPolicy(PermissionConstants.SectionCreate, policy => policy.RequireClaim("permission", PermissionConstants.SectionCreate));
    options.AddPolicy(PermissionConstants.SectionUpdate, policy => policy.RequireClaim("permission", PermissionConstants.SectionUpdate));
    options.AddPolicy(PermissionConstants.SectionDelete, policy => policy.RequireClaim("permission", PermissionConstants.SectionDelete));
    options.AddPolicy(PermissionConstants.ItemCreate, policy => policy.RequireClaim("permission", PermissionConstants.ItemCreate));
    options.AddPolicy(PermissionConstants.ItemUpdate, policy => policy.RequireClaim("permission", PermissionConstants.ItemUpdate));
    options.AddPolicy(PermissionConstants.ItemDelete, policy => policy.RequireClaim("permission", PermissionConstants.ItemDelete));
    options.AddPolicy(PermissionConstants.AuditCreate, policy => policy.RequireClaim("permission", PermissionConstants.AuditCreate));
    options.AddPolicy(PermissionConstants.AuditEdit, policy => policy.RequireClaim("permission", PermissionConstants.AuditEdit));
    options.AddPolicy(PermissionConstants.AuditSubmit, policy => policy.RequireClaim("permission", PermissionConstants.AuditSubmit));
    options.AddPolicy(PermissionConstants.AuditView, policy => policy.RequireClaim("permission", PermissionConstants.AuditView));
    options.AddPolicy(PermissionConstants.AuditViewAll, policy => policy.RequireClaim("permission", PermissionConstants.AuditViewAll));
    options.AddPolicy(PermissionConstants.ReportView, policy => policy.RequireClaim("permission", PermissionConstants.ReportView));
    options.AddPolicy(PermissionConstants.ReportExport, policy => policy.RequireClaim("permission", PermissionConstants.ReportExport));
    options.AddPolicy(PermissionConstants.UserView, policy => policy.RequireClaim("permission", PermissionConstants.UserView));
    options.AddPolicy(PermissionConstants.UserCreate, policy => policy.RequireClaim("permission", PermissionConstants.UserCreate));
    options.AddPolicy(PermissionConstants.UserUpdate, policy => policy.RequireClaim("permission", PermissionConstants.UserUpdate));
    options.AddPolicy(PermissionConstants.UserResetPassword, policy => policy.RequireClaim("permission", PermissionConstants.UserResetPassword));
    options.AddPolicy(PermissionConstants.UserPermissions, policy => policy.RequireClaim("permission", PermissionConstants.UserPermissions));
    options.AddPolicy(PermissionConstants.DatabaseBackupView, policy => policy.RequireClaim("permission", PermissionConstants.DatabaseBackupView));
    options.AddPolicy(PermissionConstants.DatabaseBackupCreate, policy => policy.RequireClaim("permission", PermissionConstants.DatabaseBackupCreate));
    options.AddPolicy(PermissionConstants.DatabaseBackupRestore, policy => policy.RequireClaim("permission", PermissionConstants.DatabaseBackupRestore));
    options.AddPolicy(PermissionConstants.DatabaseBackupSchedule, policy => policy.RequireClaim("permission", PermissionConstants.DatabaseBackupSchedule));
    options.AddPolicy("Setup.Access", policy => policy.RequireClaim(
        "permission",
        PermissionConstants.BrandView,
        PermissionConstants.BrandCreate,
        PermissionConstants.BrandUpdate,
        PermissionConstants.BrandDelete,
        PermissionConstants.BranchView,
        PermissionConstants.BranchCreate,
        PermissionConstants.BranchUpdate,
        PermissionConstants.BranchDelete,
        PermissionConstants.TemplateView,
        PermissionConstants.TemplateCreate,
        PermissionConstants.TemplateUpdate,
        PermissionConstants.TemplateDelete,
        PermissionConstants.SectionCreate,
        PermissionConstants.SectionUpdate,
        PermissionConstants.SectionDelete,
        PermissionConstants.ItemCreate,
        PermissionConstants.ItemUpdate,
        PermissionConstants.ItemDelete,
        PermissionConstants.DatabaseBackupView,
        PermissionConstants.DatabaseBackupCreate,
        PermissionConstants.DatabaseBackupRestore,
        PermissionConstants.DatabaseBackupSchedule));
    options.AddPolicy("Audit.ViewAny", policy => policy.RequireClaim(
        "permission",
        PermissionConstants.AuditView,
        PermissionConstants.AuditViewAll));
});
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthStateProvider>();

var host = builder.Build();
await SetCultureAsync(host);
await host.RunAsync();

static async Task SetCultureAsync(WebAssemblyHost host)
{
    var js = host.Services.GetRequiredService<IJSRuntime>();
    var culture = await js.InvokeAsync<string>("blazorCulture.get");
    if (string.IsNullOrWhiteSpace(culture))
    {
        culture = "ar-EG";
        await js.InvokeVoidAsync("blazorCulture.set", culture);
    }

    var cultureInfo = new CultureInfo(culture);
    CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
    CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
    await js.InvokeVoidAsync("blazorDir.set", culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr");
}
