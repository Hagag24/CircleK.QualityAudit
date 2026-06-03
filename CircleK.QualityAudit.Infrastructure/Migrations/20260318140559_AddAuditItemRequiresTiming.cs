using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleK.QualityAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditItemRequiresTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresTiming",
                table: "AuditItems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresTiming",
                table: "AuditItems");
        }
    }
}
