using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonetaryRedemptionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_RewardCatalogItemId",
                table: "Redemptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "RewardCatalogItemId",
                table: "Redemptions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<decimal>(
                name: "MonetaryAmount",
                table: "Redemptions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonetaryCurrency",
                table: "Redemptions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonetaryPointsPerPesoUnit",
                table: "Redemptions",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Redemptions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CatalogReward");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_RewardCatalogItemId",
                table: "Redemptions",
                columns: new[] { "TenantId", "RewardCatalogItemId" },
                filter: "[RewardCatalogItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_Type_RedeemedAt",
                table: "Redemptions",
                columns: new[] { "TenantId", "Type", "RedeemedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_RewardCatalogItemId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_Type_RedeemedAt",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "MonetaryAmount",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "MonetaryCurrency",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "MonetaryPointsPerPesoUnit",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Redemptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "RewardCatalogItemId",
                table: "Redemptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_RewardCatalogItemId",
                table: "Redemptions",
                columns: new[] { "TenantId", "RewardCatalogItemId" });
        }
    }
}
