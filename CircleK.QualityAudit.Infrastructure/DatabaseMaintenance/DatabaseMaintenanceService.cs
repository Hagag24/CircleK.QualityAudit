using CircleK.QualityAudit.Application.Abstractions.Services;
using CircleK.QualityAudit.Application.DatabaseMaintenance.Models;
using CircleK.QualityAudit.Domain.Entities;
using CircleK.QualityAudit.Domain.Enums;
using CircleK.QualityAudit.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;

namespace CircleK.QualityAudit.Infrastructure.DatabaseMaintenance;

public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private static readonly SemaphoreSlim OperationLock = new(1, 1);
    private static readonly SemaphoreSlim SchemaEnsureLock = new(1, 1);
    private static bool BackupSchemaEnsured;

    private readonly AppDbContext _context;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly DatabaseBackupOptions _options;
    private readonly ILogger<DatabaseMaintenanceService> _logger;

    public DatabaseMaintenanceService(
        AppDbContext context,
        IHostEnvironment hostEnvironment,
        IOptions<DatabaseBackupOptions> options,
        ILogger<DatabaseMaintenanceService> logger)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DatabaseBackupDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBackupTablesExistAsync(cancellationToken);
        var schedule = await EnsureScheduleAsync(cancellationToken);

        var records = await _context.DatabaseBackupRecords
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, _options.MaxRecords))
            .ToListAsync(cancellationToken);

        return new DatabaseBackupDashboardDto(
            GetDatabaseName(),
            GetBackupRootPath(),
            MapSchedule(schedule),
            records.Select(MapRecord).ToList());
    }

    public async Task<DatabaseBackupRecordDto> CreateBackupAsync(
        string? requestedByUserId,
        string? requestedByDisplayName,
        CreateDatabaseBackupRequest request,
        BackupTriggerType triggerType = BackupTriggerType.Manual,
        CancellationToken cancellationToken = default)
    {
        await OperationLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBackupTablesExistAsync(cancellationToken);
            return await CreateBackupCoreAsync(
                requestedByUserId,
                requestedByDisplayName,
                request,
                triggerType,
                cancellationToken);
        }
        finally
        {
            OperationLock.Release();
        }
    }

    public async Task<RestoreDatabaseBackupResult> RestoreBackupAsync(Guid backupId, CancellationToken cancellationToken = default)
    {
        await OperationLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBackupTablesExistAsync(cancellationToken);
            var backup = await _context.DatabaseBackupRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == backupId, cancellationToken);

            if (backup is null || backup.Status != DatabaseBackupStatus.Success)
            {
                return new RestoreDatabaseBackupResult(false, "Backup record not found.");
            }

            if (!File.Exists(backup.FilePath))
            {
                return new RestoreDatabaseBackupResult(false, "Backup file not found on disk.");
            }

            try
            {
                await RestoreDatabaseInternalAsync(backup.FilePath, cancellationToken);
                return new RestoreDatabaseBackupResult(true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database restore failed for backup {BackupId}", backupId);
                return new RestoreDatabaseBackupResult(false, ex.Message);
            }
        }
        finally
        {
            OperationLock.Release();
        }
    }

    public async Task<DatabaseBackupScheduleDto> UpdateScheduleAsync(UpdateDatabaseBackupScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await OperationLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBackupTablesExistAsync(cancellationToken);
            var schedule = await EnsureScheduleAsync(cancellationToken);
            var nowUtc = DateTime.UtcNow;

            schedule.IsEnabled = request.IsEnabled;
            schedule.Mode = request.Mode switch
            {
                BackupScheduleMode.IntervalHours => DatabaseBackupScheduleMode.IntervalHours,
                _ => DatabaseBackupScheduleMode.Daily
            };
            schedule.Hour = Math.Clamp(request.Hour, 0, 23);
            schedule.Minute = Math.Clamp(request.Minute, 0, 59);
            schedule.IntervalHours = Math.Clamp(request.IntervalHours, 1, 24);
            schedule.UpdatedAtUtc = nowUtc;

            schedule.NextRunAtUtc = schedule.IsEnabled
                ? ComputeNextRunUtc(schedule, nowUtc)
                : null;

            await _context.SaveChangesAsync(cancellationToken);
            return MapSchedule(schedule);
        }
        finally
        {
            OperationLock.Release();
        }
    }

    public async Task RunScheduledBackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        await OperationLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBackupTablesExistAsync(cancellationToken);
            var schedule = await EnsureScheduleAsync(cancellationToken);
            if (!schedule.IsEnabled)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            if (!schedule.NextRunAtUtc.HasValue)
            {
                schedule.NextRunAtUtc = ComputeNextRunUtc(schedule, nowUtc);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (schedule.NextRunAtUtc.Value > nowUtc)
            {
                return;
            }

            schedule.LastRunAtUtc = nowUtc;
            schedule.NextRunAtUtc = ComputeNextRunUtc(schedule, nowUtc.AddSeconds(1));
            schedule.UpdatedAtUtc = nowUtc;
            await _context.SaveChangesAsync(cancellationToken);

            await CreateBackupCoreAsync(
                requestedByUserId: null,
                requestedByDisplayName: "Scheduler",
                request: new CreateDatabaseBackupRequest("Scheduled backup"),
                triggerType: BackupTriggerType.Scheduled,
                cancellationToken: cancellationToken);
        }
        finally
        {
            OperationLock.Release();
        }
    }

    private async Task<DatabaseBackupRecordDto> CreateBackupCoreAsync(
        string? requestedByUserId,
        string? requestedByDisplayName,
        CreateDatabaseBackupRequest request,
        BackupTriggerType triggerType,
        CancellationToken cancellationToken)
    {
        var root = GetBackupRootPath();
        Directory.CreateDirectory(root);

        var nowUtc = DateTime.UtcNow;
        var fileName = $"{GetDatabaseName()}_{nowUtc:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.bak";
        var filePath = Path.Combine(root, fileName);

        var record = new DatabaseBackupRecord
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FilePath = filePath,
            CreatedAtUtc = nowUtc,
            RequestedByUserId = requestedByUserId,
            RequestedByDisplayName = requestedByDisplayName,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            TriggerType = triggerType switch
            {
                BackupTriggerType.Scheduled => DatabaseBackupTriggerType.Scheduled,
                _ => DatabaseBackupTriggerType.Manual
            },
            Status = DatabaseBackupStatus.Success,
            FileSizeBytes = 0
        };

        try
        {
            await BackupDatabaseInternalAsync(filePath, cancellationToken);
            if (File.Exists(filePath))
            {
                record.FileSizeBytes = new FileInfo(filePath).Length;
            }
        }
        catch (Exception ex)
        {
            record.Status = DatabaseBackupStatus.Failed;
            record.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Database backup failed for file {FilePath}", filePath);
        }

        _context.DatabaseBackupRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        return MapRecord(record);
    }

    private async Task<DatabaseBackupSchedule> EnsureScheduleAsync(CancellationToken cancellationToken)
    {
        DatabaseBackupSchedule? schedule;
        try
        {
            schedule = await _context.DatabaseBackupSchedules.FirstOrDefaultAsync(cancellationToken);
        }
        catch (SqlException ex) when (IsMissingBackupTableException(ex))
        {
            _logger.LogWarning(ex, "Database backup tables were missing. Recreating backup tables and retrying.");
            await EnsureBackupTablesExistAsync(cancellationToken);
            schedule = await _context.DatabaseBackupSchedules.FirstOrDefaultAsync(cancellationToken);
        }

        if (schedule is not null)
        {
            return schedule;
        }

        schedule = new DatabaseBackupSchedule
        {
            Id = Guid.NewGuid(),
            IsEnabled = false,
            Mode = DatabaseBackupScheduleMode.Daily,
            Hour = 2,
            Minute = 0,
            IntervalHours = 24,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.DatabaseBackupSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    private async Task EnsureBackupTablesExistAsync(CancellationToken cancellationToken)
    {
        if (BackupSchemaEnsured)
        {
            return;
        }

        await SchemaEnsureLock.WaitAsync(cancellationToken);
        try
        {
            if (BackupSchemaEnsured)
            {
                return;
            }

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

            await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            BackupSchemaEnsured = true;
        }
        finally
        {
            SchemaEnsureLock.Release();
        }
    }

    private static bool IsMissingBackupTableException(SqlException ex)
    {
        if (ex.Number != 208)
        {
            return false;
        }

        return ex.Message.Contains("DatabaseBackupSchedules", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("DatabaseBackupRecords", StringComparison.OrdinalIgnoreCase);
    }

    private async Task BackupDatabaseInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        var sql = $"BACKUP DATABASE [{EscapeName(GetDatabaseName())}] TO DISK = @backupPath WITH FORMAT, INIT, CHECKSUM, STATS = 5;";
        await ExecuteOnMasterAsync(sql, command =>
        {
            command.Parameters.Add(new SqlParameter("@backupPath", SqlDbType.NVarChar, 4000) { Value = filePath });
        }, cancellationToken);
    }

    private async Task RestoreDatabaseInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        var databaseName = EscapeName(GetDatabaseName());
        var setSingleUserSql = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
        var restoreSql = $"RESTORE DATABASE [{databaseName}] FROM DISK = @backupPath WITH REPLACE, RECOVERY, STATS = 5;";
        var setMultiUserSql = $"ALTER DATABASE [{databaseName}] SET MULTI_USER;";

        await ExecuteOnMasterAsync(setSingleUserSql, null, cancellationToken);
        try
        {
            await ExecuteOnMasterAsync(restoreSql, command =>
            {
                command.Parameters.Add(new SqlParameter("@backupPath", SqlDbType.NVarChar, 4000) { Value = filePath });
            }, cancellationToken);
        }
        finally
        {
            await ExecuteOnMasterAsync(setMultiUserSql, null, cancellationToken);
        }
    }

    private async Task ExecuteOnMasterAsync(
        string sql,
        Action<SqlCommand>? configure,
        CancellationToken cancellationToken)
    {
        var connectionString = BuildMasterConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 0;
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string BuildMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_context.Database.GetConnectionString() ?? _context.Database.GetDbConnection().ConnectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 30
        };

        return builder.ConnectionString;
    }

    private string GetDatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(_context.Database.GetConnectionString() ?? _context.Database.GetDbConnection().ConnectionString);
        return builder.InitialCatalog;
    }

    private string GetBackupRootPath()
    {
        var configured = _options.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "Backups";
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, configured));
    }

    private static DateTime ComputeNextRunUtc(DatabaseBackupSchedule schedule, DateTime fromUtc)
    {
        var localNow = fromUtc.ToLocalTime();
        DateTime nextLocal;

        if (schedule.Mode == DatabaseBackupScheduleMode.IntervalHours)
        {
            nextLocal = localNow.AddHours(Math.Clamp(schedule.IntervalHours, 1, 24));
        }
        else
        {
            nextLocal = new DateTime(
                localNow.Year,
                localNow.Month,
                localNow.Day,
                Math.Clamp(schedule.Hour, 0, 23),
                Math.Clamp(schedule.Minute, 0, 59),
                0,
                DateTimeKind.Local);

            if (nextLocal <= localNow)
            {
                nextLocal = nextLocal.AddDays(1);
            }
        }

        return nextLocal.ToUniversalTime();
    }

    private static string EscapeName(string value) => value.Replace("]", "]]", StringComparison.Ordinal);

    private static DatabaseBackupRecordDto MapRecord(DatabaseBackupRecord record)
    {
        return new DatabaseBackupRecordDto(
            record.Id,
            record.FileName,
            record.FilePath,
            record.FileSizeBytes,
            record.CreatedAtUtc,
            record.RequestedByUserId,
            record.RequestedByDisplayName,
            record.Note,
            record.TriggerType == DatabaseBackupTriggerType.Scheduled ? BackupTriggerType.Scheduled : BackupTriggerType.Manual,
            record.Status == DatabaseBackupStatus.Failed ? BackupRecordStatus.Failed : BackupRecordStatus.Success,
            record.ErrorMessage);
    }

    private static DatabaseBackupScheduleDto MapSchedule(DatabaseBackupSchedule schedule)
    {
        return new DatabaseBackupScheduleDto(
            schedule.Id,
            schedule.IsEnabled,
            schedule.Mode == DatabaseBackupScheduleMode.IntervalHours ? BackupScheduleMode.IntervalHours : BackupScheduleMode.Daily,
            schedule.Hour,
            schedule.Minute,
            schedule.IntervalHours,
            schedule.LastRunAtUtc,
            schedule.NextRunAtUtc,
            schedule.UpdatedAtUtc);
    }
}



