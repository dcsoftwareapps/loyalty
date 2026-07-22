using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidThroughUtcToTenantSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidThroughUtc",
                table: "TenantSubscriptions",
                type: "datetime2(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidThroughUtc",
                table: "TenantSubscriptions");
        }
    }
}
