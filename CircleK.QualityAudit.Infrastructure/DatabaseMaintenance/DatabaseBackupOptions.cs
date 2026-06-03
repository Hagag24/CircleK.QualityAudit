namespace CircleK.QualityAudit.Infrastructure.DatabaseMaintenance;

public sealed class DatabaseBackupOptions
{
    public string? RootPath { get; set; }
    public int MaxRecords { get; set; } = 200;
}
