using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailApplicationBaseUrl",
                table: "BillingSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailFromAddress",
                table: "BillingSettings",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailFromName",
                table: "BillingSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "LoyaltyCloud");

            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                table: "BillingSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmailProvider",
                table: "BillingSettings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Cloudflare");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailApplicationBaseUrl",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "EmailFromAddress",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "EmailFromName",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                table: "BillingSettings");

            migrationBuilder.DropColumn(
                name: "EmailProvider",
                table: "BillingSettings");
        }
    }
}
