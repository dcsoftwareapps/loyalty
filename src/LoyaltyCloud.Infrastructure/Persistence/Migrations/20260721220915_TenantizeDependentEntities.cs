using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantizeDependentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_PointLots_PointLotId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_PointTransactions_ConsumingPointTransactionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_Redemptions_RedemptionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLots_LoyaltyCards_LoyaltyCardId",
                table: "PointLots");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLots_PointTransactions_SourcePointTransactionId",
                table: "PointLots");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_LoyaltyCards_LoyaltyCardId",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointCampaigns_CampaignId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_CampaignId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_LoyaltyCardId_CreatedAt",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointLots_ExpiresAt_RemainingAmount",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLots_LoyaltyCardId_ExpiresAt_EarnedAt",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLots_SourcePointTransactionId",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_ConsumingPointTransactionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_PointLotId",
                table: "PointLotConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_RedemptionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_Channel",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_Status",
                table: "NotificationDeliveries");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PointTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PointLots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PointLotConsumptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "NotificationDeliveries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE pt
                SET TenantId = lc.TenantId
                FROM PointTransactions pt
                INNER JOIN LoyaltyCards lc ON pt.LoyaltyCardId = lc.Id
                WHERE pt.TenantId IS NULL;

                UPDATE pl
                SET TenantId = lc.TenantId
                FROM PointLots pl
                INNER JOIN LoyaltyCards lc ON pl.LoyaltyCardId = lc.Id
                WHERE pl.TenantId IS NULL;

                UPDATE plc
                SET TenantId = pl.TenantId
                FROM PointLotConsumptions plc
                INNER JOIN PointLots pl ON plc.PointLotId = pl.Id
                WHERE plc.TenantId IS NULL;

                UPDATE nd
                SET TenantId = ln.TenantId
                FROM NotificationDeliveries nd
                INNER JOIN LoyaltyNotifications ln ON nd.LoyaltyNotificationId = ln.Id
                WHERE nd.TenantId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "PointTransactions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "PointLots",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "PointLotConsumptions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "NotificationDeliveries",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Redemptions_TenantId_Id",
                table: "Redemptions",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PointTransactions_TenantId_Id",
                table: "PointTransactions",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PointLots_TenantId_Id",
                table: "PointLots",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PointCampaigns_TenantId_Id",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LoyaltyNotifications_TenantId_Id",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_TenantId_CampaignId",
                table: "PointTransactions",
                columns: new[] { "TenantId", "CampaignId" },
                filter: "[CampaignId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_TenantId_LoyaltyCardId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "TenantId", "LoyaltyCardId", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_TenantId_Type_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "TenantId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_TenantId_ExpiresAt_RemainingAmount",
                table: "PointLots",
                columns: new[] { "TenantId", "ExpiresAt", "RemainingAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_TenantId_LoyaltyCardId_ExpiresAt_EarnedAt",
                table: "PointLots",
                columns: new[] { "TenantId", "LoyaltyCardId", "ExpiresAt", "EarnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_TenantId_SourcePointTransactionId",
                table: "PointLots",
                columns: new[] { "TenantId", "SourcePointTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_TenantId_ConsumingPointTransactionId",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "ConsumingPointTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_TenantId_CreatedAt",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_TenantId_PointLotId",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "PointLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_TenantId_RedemptionId",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "RedemptionId" },
                filter: "[RedemptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_TenantId_Channel",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "LoyaltyNotificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_TenantId_Status",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries",
                columns: new[] { "TenantId", "LoyaltyNotificationId" },
                principalTable: "LoyaltyNotifications",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_Tenants_TenantId",
                table: "NotificationDeliveries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_PointLots_TenantId_PointLotId",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "PointLotId" },
                principalTable: "PointLots",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_PointTransactions_TenantId_ConsumingPointTransactionId",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "ConsumingPointTransactionId" },
                principalTable: "PointTransactions",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_Redemptions_TenantId_RedemptionId",
                table: "PointLotConsumptions",
                columns: new[] { "TenantId", "RedemptionId" },
                principalTable: "Redemptions",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_Tenants_TenantId",
                table: "PointLotConsumptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLots_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "PointLots",
                columns: new[] { "TenantId", "LoyaltyCardId" },
                principalTable: "LoyaltyCards",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLots_PointTransactions_TenantId_SourcePointTransactionId",
                table: "PointLots",
                columns: new[] { "TenantId", "SourcePointTransactionId" },
                principalTable: "PointTransactions",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLots_Tenants_TenantId",
                table: "PointLots",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "PointTransactions",
                columns: new[] { "TenantId", "LoyaltyCardId" },
                principalTable: "LoyaltyCards",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointCampaigns_TenantId_CampaignId",
                table: "PointTransactions",
                columns: new[] { "TenantId", "CampaignId" },
                principalTable: "PointCampaigns",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_Tenants_TenantId",
                table: "PointTransactions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_LoyaltyNotifications_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_Tenants_TenantId",
                table: "NotificationDeliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_PointLots_TenantId_PointLotId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_PointTransactions_TenantId_ConsumingPointTransactionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_Redemptions_TenantId_RedemptionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLotConsumptions_Tenants_TenantId",
                table: "PointLotConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLots_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "PointLots");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLots_PointTransactions_TenantId_SourcePointTransactionId",
                table: "PointLots");

            migrationBuilder.DropForeignKey(
                name: "FK_PointLots_Tenants_TenantId",
                table: "PointLots");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointCampaigns_TenantId_CampaignId",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_Tenants_TenantId",
                table: "PointTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Redemptions_TenantId_Id",
                table: "Redemptions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PointTransactions_TenantId_Id",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_TenantId_CampaignId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_TenantId_LoyaltyCardId_CreatedAt",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_TenantId_Type_CreatedAt",
                table: "PointTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PointLots_TenantId_Id",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLots_TenantId_ExpiresAt_RemainingAmount",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLots_TenantId_LoyaltyCardId_ExpiresAt_EarnedAt",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLots_TenantId_SourcePointTransactionId",
                table: "PointLots");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_TenantId_ConsumingPointTransactionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_TenantId_CreatedAt",
                table: "PointLotConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_TenantId_PointLotId",
                table: "PointLotConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_PointLotConsumptions_TenantId_RedemptionId",
                table: "PointLotConsumptions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PointCampaigns_TenantId_Id",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_TenantId_Channel",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_TenantId_LoyaltyNotificationId",
                table: "NotificationDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_TenantId_Status",
                table: "NotificationDeliveries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LoyaltyNotifications_TenantId_Id",
                table: "LoyaltyNotifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PointLots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PointLotConsumptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NotificationDeliveries");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CampaignId",
                table: "PointTransactions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_LoyaltyCardId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "LoyaltyCardId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_ExpiresAt_RemainingAmount",
                table: "PointLots",
                columns: new[] { "ExpiresAt", "RemainingAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_LoyaltyCardId_ExpiresAt_EarnedAt",
                table: "PointLots",
                columns: new[] { "LoyaltyCardId", "ExpiresAt", "EarnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_SourcePointTransactionId",
                table: "PointLots",
                column: "SourcePointTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_ConsumingPointTransactionId",
                table: "PointLotConsumptions",
                column: "ConsumingPointTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_PointLotId",
                table: "PointLotConsumptions",
                column: "PointLotId");

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_RedemptionId",
                table: "PointLotConsumptions",
                column: "RedemptionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Channel",
                table: "NotificationDeliveries",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status",
                table: "NotificationDeliveries",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_PointLots_PointLotId",
                table: "PointLotConsumptions",
                column: "PointLotId",
                principalTable: "PointLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_PointTransactions_ConsumingPointTransactionId",
                table: "PointLotConsumptions",
                column: "ConsumingPointTransactionId",
                principalTable: "PointTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLotConsumptions_Redemptions_RedemptionId",
                table: "PointLotConsumptions",
                column: "RedemptionId",
                principalTable: "Redemptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLots_LoyaltyCards_LoyaltyCardId",
                table: "PointLots",
                column: "LoyaltyCardId",
                principalTable: "LoyaltyCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointLots_PointTransactions_SourcePointTransactionId",
                table: "PointLots",
                column: "SourcePointTransactionId",
                principalTable: "PointTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_LoyaltyCards_LoyaltyCardId",
                table: "PointTransactions",
                column: "LoyaltyCardId",
                principalTable: "LoyaltyCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointCampaigns_CampaignId",
                table: "PointTransactions",
                column: "CampaignId",
                principalTable: "PointCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
