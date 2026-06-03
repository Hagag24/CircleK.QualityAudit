using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleK.QualityAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeCount",
                table: "Branches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentEmployeeCount",
                table: "AuditSessions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeCount",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "CurrentEmployeeCount",
                table: "AuditSessions");
        }
    }
}
