using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Domain.Interfaces;
using CircleK.QualityAudit.Infrastructure.Audits;
using CircleK.QualityAudit.Infrastructure.DatabaseMaintenance;
using CircleK.QualityAudit.Infrastructure.Identity;
using CircleK.QualityAudit.Infrastructure.Persistence;
using CircleK.QualityAudit.Infrastructure.Persistence.Repositories;
using CircleK.QualityAudit.Infrastructure.Reports;
using CircleK.QualityAudit.Infrastructure.Setup;
using CircleK.QualityAudit.Infrastructure.Users;
using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CircleK.QualityAudit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
            "Server=DESKTOP-R0HRFPD\\SQLEXPRESS;Database=CircleK.QualityAudit;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(60);
            }));

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.Configure<DatabaseBackupOptions>(configuration.GetSection("DatabaseBackup"));
        services.AddHostedService<DatabaseBackupScheduler>();

        return services;
    }
}
