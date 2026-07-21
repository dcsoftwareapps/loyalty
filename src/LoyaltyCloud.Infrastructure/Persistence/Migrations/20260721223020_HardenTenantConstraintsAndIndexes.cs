using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenTenantConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_LoyaltyNotificationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_Id",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_LoyaltyCardId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_TenantId_Id",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_LoyaltyNotificationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_TenantId_Channel",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_Id",
                table: "LoyaltyNotifications");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LoyaltyCards_TenantId_SerialNumber",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "SerialNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_TenantId_MinLevel",
                table: "RewardCatalogItems",
                columns: new[] { "TenantId", "MinLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_LoyaltyCardId_RedeemedAt",
                table: "Redemptions",
                columns: new[] { "TenantId", "LoyaltyCardId", "RedeemedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_TenantId_Channel_Status",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "Channel", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_Status_ScheduledAtUtc",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_IsActive",
                table: "Customers",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceRegistrations_LoyaltyCards_TenantId_SerialNumber",
                table: "DeviceRegistrations",
                columns: new[] { "TenantId", "SerialNumber" },
                principalTable: "LoyaltyCards",
                principalColumns: new[] { "TenantId", "SerialNumber" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "LoyaltyNotificationId" },
                principalTable: "LoyaltyNotifications",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceRegistrations_LoyaltyCards_TenantId_SerialNumber",
                table: "DeviceRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_RewardCatalogItems_TenantId_MinLevel",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_LoyaltyCardId_RedeemedAt",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_TenantId_Channel_Status",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_Status_ScheduledAtUtc",
                table: "LoyaltyNotifications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LoyaltyCards_TenantId_SerialNumber",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_IsActive",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_Id",
                table: "Redemptions",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_LoyaltyCardId",
                table: "Redemptions",
                columns: new[] { "TenantId", "LoyaltyCardId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_TenantId_Id",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                column: "LoyaltyNotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_TenantId_Channel",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_Id",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                column: "LoyaltyNotificationId",
                principalTable: "LoyaltyNotifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "LoyaltyNotificationId" },
                principalTable: "LoyaltyNotifications",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
