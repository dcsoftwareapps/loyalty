using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KBeauty.Loyalty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomNotificationCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomNotificationCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ShortMessage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LongMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AudienceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinimumPoints = table.Column<int>(type: "int", nullable: true),
                    PointsExpiringDaysAhead = table.Column<int>(type: "int", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DisplayUntilUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IntendedRecipients = table.Column<int>(type: "int", nullable: false),
                    NotificationsCreated = table.Column<int>(type: "int", nullable: false),
                    NotificationsSucceeded = table.Column<int>(type: "int", nullable: false),
                    NotificationsFailed = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomNotificationCampaigns", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LongMessage",
                table: "LoyaltyNotifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortMessage",
                table: "LoyaltyNotifications",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                column: "CustomNotificationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_CreatedAt",
                table: "CustomNotificationCampaigns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_ScheduledAtUtc",
                table: "CustomNotificationCampaigns",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_Status",
                table: "CustomNotificationCampaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_Status_ScheduledAtUtc",
                table: "CustomNotificationCampaigns",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_CustomNotificationCampaigns_CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                column: "CustomNotificationCampaignId",
                principalTable: "CustomNotificationCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_CustomNotificationCampaigns_CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropTable(
                name: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropColumn(
                name: "CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropColumn(
                name: "LongMessage",
                table: "LoyaltyNotifications");

            migrationBuilder.DropColumn(
                name: "ShortMessage",
                table: "LoyaltyNotifications");
        }
    }
}
