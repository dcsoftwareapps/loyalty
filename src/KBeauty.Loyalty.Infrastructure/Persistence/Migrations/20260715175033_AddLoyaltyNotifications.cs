using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KBeauty.Loyalty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DisplayUntilUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyNotifications_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyNotifications_LoyaltyCards_LoyaltyCardId",
                        column: x => x.LoyaltyCardId,
                        principalTable: "LoyaltyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DevicesFound = table.Column<int>(type: "int", nullable: false),
                    PushesAttempted = table.Column<int>(type: "int", nullable: false),
                    PushesAccepted = table.Column<int>(type: "int", nullable: false),
                    PushesFailed = table.Column<int>(type: "int", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_LoyaltyNotifications_LoyaltyNotificationId",
                        column: x => x.LoyaltyNotificationId,
                        principalTable: "LoyaltyNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_CorrelationId",
                table: "LoyaltyNotifications",
                column: "CorrelationId",
                unique: true,
                filter: "[CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_CreatedAt",
                table: "LoyaltyNotifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_CustomerId",
                table: "LoyaltyNotifications",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_LoyaltyCardId",
                table: "LoyaltyNotifications",
                column: "LoyaltyCardId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_ScheduledAtUtc",
                table: "LoyaltyNotifications",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_Status",
                table: "LoyaltyNotifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Channel",
                table: "NotificationDeliveries",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                column: "LoyaltyNotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status",
                table: "NotificationDeliveries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "LoyaltyNotifications");
        }
    }
}
