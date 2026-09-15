using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRedemptionIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Redemptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_IdempotencyKey",
                table: "Redemptions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_IdempotencyKey",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Redemptions");
        }
    }
}
