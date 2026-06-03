using CircleK.QualityAudit.Application;
using CircleK.QualityAudit.Infrastructure;
using CircleK.QualityAudit.Infrastructure.Identity;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".CircleK.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionConstants.All)
    {
        options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
    }

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(
                "https://localhost:7249",
                "http://localhost:5137",
                "https://localhost:7001",
                "https://app.circlek.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("BlazorClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await SeedData.SeedAsync(app.Services);

app.Run();
