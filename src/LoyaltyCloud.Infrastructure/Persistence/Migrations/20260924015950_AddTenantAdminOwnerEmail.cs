using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAdminOwnerEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "TenantAdminUsers",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "TenantAdminUsers",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantAdminUsers_TenantId_NormalizedEmail",
                table: "TenantAdminUsers",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantAdminUsers_TenantId_NormalizedEmail",
                table: "TenantAdminUsers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "TenantAdminUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "TenantAdminUsers");
        }
    }
}
