using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleK.QualityAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTimingMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditTimingMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTimingMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditTimingMeasurements_AuditItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "AuditItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditTimingMeasurements_AuditSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AuditSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTimingMeasurements_ItemId",
                table: "AuditTimingMeasurements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTimingMeasurements_SessionId_ItemId",
                table: "AuditTimingMeasurements",
                columns: new[] { "SessionId", "ItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditTimingMeasurements");
        }
    }
}
