using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CircleK.QualityAudit.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AuditTemplate> AuditTemplates => Set<AuditTemplate>();
    public DbSet<AuditSection> AuditSections => Set<AuditSection>();
    public DbSet<AuditItem> AuditItems => Set<AuditItem>();
    public DbSet<AuditSession> AuditSessions => Set<AuditSession>();
    public DbSet<AuditAnswer> AuditAnswers => Set<AuditAnswer>();
    public DbSet<AuditTimingMeasurement> AuditTimingMeasurements => Set<AuditTimingMeasurement>();
    public DbSet<DatabaseBackupRecord> DatabaseBackupRecords => Set<DatabaseBackupRecord>();
    public DbSet<DatabaseBackupSchedule> DatabaseBackupSchedules => Set<DatabaseBackupSchedule>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Brand>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.NameAr).HasMaxLength(200);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
        });

        builder.Entity<Branch>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.NameAr).HasMaxLength(200);
            b.Property(x => x.Region).HasMaxLength(200);
            b.Property(x => x.ManagerName).HasMaxLength(200);
            b.Property(x => x.EmployeeCount);
            b.HasOne(x => x.Brand)
                .WithMany(x => x.Branches)
                .HasForeignKey(x => x.BrandId);
        });

        builder.Entity<AuditTemplate>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasOne(x => x.Brand)
                .WithMany(x => x.Templates)
                .HasForeignKey(x => x.BrandId);
        });

        builder.Entity<AuditSection>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.NameAr).HasMaxLength(200);
            b.HasOne(x => x.Template)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.TemplateId);
        });

        builder.Entity<AuditItem>(b =>
        {
            b.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            b.Property(x => x.TextAr).HasMaxLength(1000);
            b.Property(x => x.RequiresTiming).HasDefaultValue(false);
            b.HasOne(x => x.Section)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SectionId);
        });

        builder.Entity<AuditSession>(b =>
        {
            b.Property(x => x.CurrentEmployeeCount);
            b.HasOne(x => x.Branch)
                .WithMany(x => x.AuditSessions)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Template)
                .WithMany()
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Inspector)
                .WithMany()
                .HasForeignKey(x => x.InspectorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditAnswer>(b =>
        {
            b.HasOne(x => x.Session)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.SessionId);

            b.HasOne(x => x.Item)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.ItemId);
        });

        builder.Entity<AuditTimingMeasurement>(b =>
        {
            b.Property(x => x.DurationSeconds).IsRequired();
            b.Property(x => x.RecordedAt).IsRequired();
            b.HasOne(x => x.Session)
                .WithMany(x => x.Timings)
                .HasForeignKey(x => x.SessionId);

            b.HasOne(x => x.Item)
                .WithMany(x => x.Timings)
                .HasForeignKey(x => x.ItemId);

            b.HasIndex(x => new { x.SessionId, x.ItemId });
        });

        builder.Entity<DatabaseBackupRecord>(b =>
        {
            b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            b.Property(x => x.FilePath).HasMaxLength(1024).IsRequired();
            b.Property(x => x.RequestedByUserId).HasMaxLength(450);
            b.Property(x => x.RequestedByDisplayName).HasMaxLength(200);
            b.Property(x => x.Note).HasMaxLength(500);
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.TriggerType).HasConversion<int>();
            b.HasIndex(x => x.CreatedAtUtc);
        });

        builder.Entity<DatabaseBackupSchedule>(b =>
        {
            b.Property(x => x.Mode).HasConversion<int>();
        });

        builder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.Property(x => x.Permission).HasMaxLength(200).IsRequired();
            b.HasOne(x => x.User)
                .WithMany(x => x.Permissions)
                .HasForeignKey(x => x.UserId);
        });

        builder.Entity<AppUser>(b =>
        {
            b.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
