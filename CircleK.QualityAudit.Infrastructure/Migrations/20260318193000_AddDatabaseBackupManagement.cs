using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleK.QualityAudit.Infrastructure.Migrations;

[Migration("20260318193000_AddDatabaseBackupManagement")]
public partial class AddDatabaseBackupManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DatabaseBackupRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                RequestedByDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                TriggerType = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatabaseBackupRecords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DatabaseBackupSchedules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                Mode = table.Column<int>(type: "int", nullable: false),
                Hour = table.Column<int>(type: "int", nullable: false),
                Minute = table.Column<int>(type: "int", nullable: false),
                IntervalHours = table.Column<int>(type: "int", nullable: false),
                LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatabaseBackupSchedules", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DatabaseBackupRecords_CreatedAtUtc",
            table: "DatabaseBackupRecords",
            column: "CreatedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DatabaseBackupRecords");

        migrationBuilder.DropTable(
            name: "DatabaseBackupSchedules");
    }
}
