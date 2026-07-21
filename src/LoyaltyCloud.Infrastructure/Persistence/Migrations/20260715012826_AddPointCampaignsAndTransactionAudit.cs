using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPointCampaignsAndTransactionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedMultiplier",
                table: "PointTransactions",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BasePoints",
                table: "PointTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "PointTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PointCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Multiplier = table.Column<int>(type: "int", nullable: false),
                    MinimumPurchaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LevelEligibility = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointCampaigns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CampaignId",
                table: "PointTransactions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_EndsAtUtc",
                table: "PointCampaigns",
                column: "EndsAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_IsActive",
                table: "PointCampaigns",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_IsActive_StartsAtUtc_EndsAtUtc",
                table: "PointCampaigns",
                columns: new[] { "IsActive", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_StartsAtUtc",
                table: "PointCampaigns",
                column: "StartsAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointCampaigns_CampaignId",
                table: "PointTransactions",
                column: "CampaignId",
                principalTable: "PointCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointCampaigns_CampaignId",
                table: "PointTransactions");

            migrationBuilder.DropTable(
                name: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_CampaignId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "AppliedMultiplier",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "BasePoints",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "PointTransactions");
        }
    }
}
