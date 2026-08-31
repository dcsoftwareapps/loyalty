using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppleWalletPrimaryContentMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppleWalletPrimaryContentMode",
                table: "TenantBrandings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CustomerName");

            migrationBuilder.AddColumn<string>(
                name: "AppleWalletStripImageBlobName",
                table: "TenantBrandings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppleWalletPrimaryContentMode",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "AppleWalletStripImageBlobName",
                table: "TenantBrandings");
        }
    }
}
