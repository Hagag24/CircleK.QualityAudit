using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Infrastructure.Identity;
using CircleK.QualityAudit.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CircleK.QualityAudit.Infrastructure;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        await context.Database.MigrateAsync();
        await EnsureDatabaseBackupTablesExistAsync(context);

        var roles = new[] { "Admin", "QualityManager", "Inspector", "Viewer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = "admin@circlek.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "System Admin",
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(admin, "Admin@123");
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        if (admin is not null)
        {
            var existingPermissions = await context.UserPermissions
                .Where(p => p.UserId == admin.Id)
                .Select(p => p.Permission)
                .ToListAsync();

            var missing = PermissionConstants.All.Except(existingPermissions).ToList();
            if (missing.Count > 0)
            {
                context.UserPermissions.AddRange(missing.Select(p => new UserPermission
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
                    Permission = p
                }));
                await context.SaveChangesAsync();
            }
        }

        var brand = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Circle K");
        if (brand is null)
        {
            brand = new Brand
            {
                Id = Guid.NewGuid(),
                Name = "Circle K",
                NameAr = "سيركل كيه",
                IsActive = true
            };

            context.Brands.Add(brand);
            await context.SaveChangesAsync();
        }

        if (!await context.Branches.AnyAsync())
        {
            var branch1 = new Branch
            {
                Id = Guid.NewGuid(),
                BrandId = brand.Id,
                Name = "Cairo - Maadi",
                NameAr = "القاهرة - المعادي",
                Region = "Cairo",
                ManagerName = "Ahmed Hassan",
                EmployeeCount = 18,
                IsActive = true
            };

            var branch2 = new Branch
            {
                Id = Guid.NewGuid(),
                BrandId = brand.Id,
                Name = "Alexandria - Sidi Gaber",
                NameAr = "الإسكندرية - سيدي جابر",
                Region = "Alexandria",
                ManagerName = "Sara Mohamed",
                EmployeeCount = 14,
                IsActive = true
            };

            context.Branches.AddRange(branch1, branch2);
            await context.SaveChangesAsync();
        }

        var hasImportedTemplate = await context.AuditTemplates.AnyAsync(t => t.Name == "Circle K Quality Audit (Imported)");
        if (!hasImportedTemplate)
        {
            var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            var excelPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "Opportunitiess & quality report.xlsx"));
            var imported = await TryImportTemplateFromExcelAsync(context, brand, excelPath);

            if (!imported && !await context.AuditTemplates.AnyAsync())
            {
                var template = new AuditTemplate
                {
                    Id = Guid.NewGuid(),
                    BrandId = brand.Id,
                    Name = "Circle K Quality Audit v1",
                    Version = 1,
                    IsActive = true
                };

                var section = new AuditSection
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Name = "Food Safety",
                    NameAr = "سلامة الغذاء",
                    Order = 1
                };

                var item1 = new AuditItem
                {
                    Id = Guid.NewGuid(),
                    SectionId = section.Id,
                    Text = "All staff wash hands every hour",
                    TextAr = "يقوم جميع العاملين بغسيل الأيدي كل ساعة",
                    WOP = 15,
                    IsCritical = false,
                    Order = 1
                };

                var item2 = new AuditItem
                {
                    Id = Guid.NewGuid(),
                    SectionId = section.Id,
                    Text = "Valid health certificates are available",
                    TextAr = "الشهادات الصحية متاحة وسارية",
                    WOP = 15,
                    IsCritical = true,
                    Order = 2
                };

                context.AuditTemplates.Add(template);
                context.AuditSections.Add(section);
                context.AuditItems.AddRange(item1, item2);

                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<bool> TryImportTemplateFromExcelAsync(AppDbContext context, Brand brand, string excelPath)
    {
        if (!File.Exists(excelPath))
        {
            return false;
        }

        using var workbook = new XLWorkbook(excelPath);
        var sheet = workbook.Worksheets.FirstOrDefault(ws => string.Equals(ws.Name.Trim(), "Report", StringComparison.OrdinalIgnoreCase));
        if (sheet is null)
        {
            return false;
        }

        var used = sheet.RangeUsed();
        if (used is null)
        {
            return false;
        }

        var template = new AuditTemplate
        {
            Id = Guid.NewGuid(),
            BrandId = brand.Id,
            Name = "Circle K Quality Audit (Imported)",
            Version = 1,
            IsActive = true
        };

        var sections = new List<AuditSection>();
        var items = new List<AuditItem>();
        var sectionOrder = 0;
        var itemOrder = 0;
        AuditSection? currentSection = null;
        var started = false;

        foreach (var row in used.RowsUsed())
        {
            var col1 = row.Cell(1).GetString().Trim();
            var col2 = row.Cell(2).GetString().Trim();
            var col3 = row.Cell(3).GetString().Trim();

            if (!started)
            {
                if (string.Equals(col1, "#", StringComparison.OrdinalIgnoreCase) &&
                    (col2.Contains("القسم", StringComparison.OrdinalIgnoreCase) || col2.Contains("SECTION", StringComparison.OrdinalIgnoreCase)))
                {
                    started = true;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(col2))
            {
                continue;
            }

            if (IsSummaryRow(col2))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(col1) && string.IsNullOrWhiteSpace(col3))
            {
                sectionOrder++;
                itemOrder = 0;
                currentSection = new AuditSection
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Name = col2,
                    NameAr = col2,
                    Order = sectionOrder
                };
                sections.Add(currentSection);
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            if (!int.TryParse(col1, out _))
            {
                continue;
            }

            itemOrder++;
            var wop = ParseWop(row.Cell(3));
            var text = col2;

            items.Add(new AuditItem
            {
                Id = Guid.NewGuid(),
                SectionId = currentSection.Id,
                Text = text,
                TextAr = text,
                WOP = wop,
                IsCritical = false,
                RequiresTiming = false,
                Order = itemOrder
            });
        }

        if (sections.Count == 0 || items.Count == 0)
        {
            return false;
        }

        context.AuditTemplates.Add(template);
        context.AuditSections.AddRange(sections);
        context.AuditItems.AddRange(items);
        await context.SaveChangesAsync();

        return true;
    }

    private static async Task EnsureDatabaseBackupTablesExistAsync(AppDbContext context)
    {
        const string sql = """
IF OBJECT_ID(N'[DatabaseBackupSchedules]', N'U') IS NULL
BEGIN
    CREATE TABLE [DatabaseBackupSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Mode] int NOT NULL,
        [Hour] int NOT NULL,
        [Minute] int NOT NULL,
        [IntervalHours] int NOT NULL,
        [LastRunAtUtc] datetime2 NULL,
        [NextRunAtUtc] datetime2 NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DatabaseBackupSchedules] PRIMARY KEY ([Id])
    );
END;

IF OBJECT_ID(N'[DatabaseBackupRecords]', N'U') IS NULL
BEGIN
    CREATE TABLE [DatabaseBackupRecords] (
        [Id] uniqueidentifier NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [FilePath] nvarchar(1024) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [RequestedByUserId] nvarchar(450) NULL,
        [RequestedByDisplayName] nvarchar(200) NULL,
        [Note] nvarchar(500) NULL,
        [TriggerType] int NOT NULL,
        [Status] int NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        CONSTRAINT [PK_DatabaseBackupRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_DatabaseBackupRecords_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'[DatabaseBackupRecords]')
)
BEGIN
    CREATE INDEX [IX_DatabaseBackupRecords_CreatedAtUtc]
        ON [DatabaseBackupRecords]([CreatedAtUtc]);
END;
""";

        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static bool IsSummaryRow(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        return trimmed.StartsWith("مجموع", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("اجمالى", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("إجمالي", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("إجمالى", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseWop(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Number)
        {
            return (int)Math.Round(cell.GetDouble());
        }

        var raw = cell.GetString();
        return int.TryParse(raw, out var value) ? value : 0;
    }
}
