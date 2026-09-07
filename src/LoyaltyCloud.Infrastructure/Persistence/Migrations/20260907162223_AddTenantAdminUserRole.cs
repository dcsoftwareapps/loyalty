using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAdminUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "TenantAdminUsers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Admin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "TenantAdminUsers");
        }
    }
}
