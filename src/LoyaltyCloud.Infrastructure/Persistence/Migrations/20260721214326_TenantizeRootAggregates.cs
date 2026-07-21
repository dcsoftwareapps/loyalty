using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantizeRootAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyCards_Customers_CustomerId",
                table: "LoyaltyCards");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_CustomNotificationCampaigns_CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_Customers_CustomerId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_LoyaltyCards_LoyaltyCardId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Redemptions_LoyaltyCards_LoyaltyCardId",
                table: "Redemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Redemptions_RewardCatalogItems_RewardCatalogItemId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_TenantAdminUsers_TenantId_Username",
                table: "TenantAdminUsers");

            migrationBuilder.DropIndex(
                name: "IX_RewardCatalogItems_IsActive",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_RewardCatalogItems_IsMonthlyProduct",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_LoyaltyCardId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_RewardCatalogItemId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_Status",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_ProgramConfigs_Key",
                table: "ProgramConfigs");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_EndsAtUtc",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_IsActive",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_IsActive_StartsAtUtc_EndsAtUtc",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_StartsAtUtc",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_CorrelationId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_CreatedAt",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_CustomerId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_LoyaltyCardId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_ScheduledAtUtc",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_Status",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_CustomerId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_DeviceRegistrations_SerialNumber",
                table: "DeviceRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_CreatedAt",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_ScheduledAtUtc",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_Status",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_Status_ScheduledAtUtc",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ReferredBy",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUsername",
                table: "TenantAdminUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RewardCatalogItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Redemptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProgramConfigs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PointCampaigns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LoyaltyNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LoyaltyCards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeviceRegistrations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CustomNotificationCampaigns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "Customers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE TenantAdminUsers
                SET NormalizedUsername = UPPER(LTRIM(RTRIM(Username)))
                WHERE NormalizedUsername IS NULL OR NormalizedUsername = '';

                UPDATE RewardCatalogItems
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE Redemptions
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE ProgramConfigs
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE PointCampaigns
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE LoyaltyNotifications
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE LoyaltyCards
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE DeviceRegistrations
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE CustomNotificationCampaigns
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;

                UPDATE Customers
                SET TenantId = 'b1000000-0000-0000-0000-000000000001'
                WHERE TenantId IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUsername",
                table: "TenantAdminUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "RewardCatalogItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Redemptions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "ProgramConfigs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "PointCampaigns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "LoyaltyNotifications",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "LoyaltyCards",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "DeviceRegistrations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "CustomNotificationCampaigns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RewardCatalogItems_TenantId_Id",
                table: "RewardCatalogItems",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LoyaltyCards_TenantId_Id",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CustomNotificationCampaigns_TenantId_Id",
                table: "CustomNotificationCampaigns",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Customers_TenantId_Id",
                table: "Customers",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000005"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000006"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000007"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000008"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000009"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000a"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000b"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000c"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000d"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000e"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000f"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000010"),
                column: "TenantId",
                value: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_TenantAdminUsers_TenantId_NormalizedUsername",
                table: "TenantAdminUsers",
                columns: new[] { "TenantId", "NormalizedUsername" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_TenantId_IsActive",
                table: "RewardCatalogItems",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_TenantId_IsMonthlyProduct",
                table: "RewardCatalogItems",
                columns: new[] { "TenantId", "IsMonthlyProduct" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_TenantId_IsMonthlyProduct_IsActive_ValidFrom_ValidTo",
                table: "RewardCatalogItems",
                columns: new[] { "TenantId", "IsMonthlyProduct", "IsActive", "ValidFrom", "ValidTo" });

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
                name: "IX_Redemptions_TenantId_RewardCatalogItemId",
                table: "Redemptions",
                columns: new[] { "TenantId", "RewardCatalogItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TenantId_Status_RedeemedAt",
                table: "Redemptions",
                columns: new[] { "TenantId", "Status", "RedeemedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramConfigs_TenantId_Key",
                table: "ProgramConfigs",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_TenantId_EndsAtUtc",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_TenantId_Id",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_TenantId_IsActive",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_TenantId_IsActive_StartsAtUtc_EndsAtUtc",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "IsActive", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PointCampaigns_TenantId_StartsAtUtc",
                table: "PointCampaigns",
                columns: new[] { "TenantId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_CorrelationId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "CorrelationId" },
                unique: true,
                filter: "[CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_CreatedAt",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_CustomerId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "CustomNotificationCampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_Id",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_LoyaltyCardId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "LoyaltyCardId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_ScheduledAtUtc",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyNotifications_TenantId_Status",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_TenantId_CustomerId",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_TenantId_DeviceLibraryIdentifier",
                table: "DeviceRegistrations",
                columns: new[] { "TenantId", "DeviceLibraryIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_TenantId_SerialNumber",
                table: "DeviceRegistrations",
                columns: new[] { "TenantId", "SerialNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_CreatedAt",
                table: "CustomNotificationCampaigns",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_ScheduledAtUtc",
                table: "CustomNotificationCampaigns",
                columns: new[] { "TenantId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_Status",
                table: "CustomNotificationCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_Status_ScheduledAtUtc",
                table: "CustomNotificationCampaigns",
                columns: new[] { "TenantId", "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Email",
                table: "Customers",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL AND [Email] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_NormalizedPhone",
                table: "Customers",
                columns: new[] { "TenantId", "NormalizedPhone" },
                unique: true,
                filter: "[NormalizedPhone] IS NOT NULL AND [NormalizedPhone] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_ReferredBy",
                table: "Customers",
                columns: new[] { "TenantId", "ReferredBy" });

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Tenants_TenantId",
                table: "Customers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomNotificationCampaigns_Tenants_TenantId",
                table: "CustomNotificationCampaigns",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceRegistrations_Tenants_TenantId",
                table: "DeviceRegistrations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyCards_Customers_TenantId_CustomerId",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyCards_Tenants_TenantId",
                table: "LoyaltyCards",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_CustomNotificationCampaigns_TenantId_CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "CustomNotificationCampaignId" },
                principalTable: "CustomNotificationCampaigns",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_Customers_TenantId_CustomerId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "LoyaltyNotifications",
                columns: new[] { "TenantId", "LoyaltyCardId" },
                principalTable: "LoyaltyCards",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_Tenants_TenantId",
                table: "LoyaltyNotifications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointCampaigns_Tenants_TenantId",
                table: "PointCampaigns",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramConfigs_Tenants_TenantId",
                table: "ProgramConfigs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Redemptions_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "Redemptions",
                columns: new[] { "TenantId", "LoyaltyCardId" },
                principalTable: "LoyaltyCards",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Redemptions_RewardCatalogItems_TenantId_RewardCatalogItemId",
                table: "Redemptions",
                columns: new[] { "TenantId", "RewardCatalogItemId" },
                principalTable: "RewardCatalogItems",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Redemptions_Tenants_TenantId",
                table: "Redemptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardCatalogItems_Tenants_TenantId",
                table: "RewardCatalogItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Tenants_TenantId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomNotificationCampaigns_Tenants_TenantId",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceRegistrations_Tenants_TenantId",
                table: "DeviceRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyCards_Customers_TenantId_CustomerId",
                table: "LoyaltyCards");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyCards_Tenants_TenantId",
                table: "LoyaltyCards");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_CustomNotificationCampaigns_TenantId_CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_Customers_TenantId_CustomerId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyNotifications_Tenants_TenantId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_PointCampaigns_Tenants_TenantId",
                table: "PointCampaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramConfigs_Tenants_TenantId",
                table: "ProgramConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_Redemptions_LoyaltyCards_TenantId_LoyaltyCardId",
                table: "Redemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Redemptions_RewardCatalogItems_TenantId_RewardCatalogItemId",
                table: "Redemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Redemptions_Tenants_TenantId",
                table: "Redemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardCatalogItems_Tenants_TenantId",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_TenantAdminUsers_TenantId_NormalizedUsername",
                table: "TenantAdminUsers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RewardCatalogItems_TenantId_Id",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_RewardCatalogItems_TenantId_IsActive",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_RewardCatalogItems_TenantId_IsMonthlyProduct",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_RewardCatalogItems_TenantId_IsMonthlyProduct_IsActive_ValidFrom_ValidTo",
                table: "RewardCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_Id",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_LoyaltyCardId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_RewardCatalogItemId",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_TenantId_Status_RedeemedAt",
                table: "Redemptions");

            migrationBuilder.DropIndex(
                name: "IX_ProgramConfigs_TenantId_Key",
                table: "ProgramConfigs");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_TenantId_EndsAtUtc",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_TenantId_Id",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_TenantId_IsActive",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_TenantId_IsActive_StartsAtUtc_EndsAtUtc",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_PointCampaigns_TenantId_StartsAtUtc",
                table: "PointCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_CorrelationId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_CreatedAt",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_CustomerId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_CustomNotificationCampaignId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_Id",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_LoyaltyCardId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_ScheduledAtUtc",
                table: "LoyaltyNotifications");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyNotifications_TenantId_Status",
                table: "LoyaltyNotifications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LoyaltyCards_TenantId_Id",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_TenantId_CustomerId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_DeviceRegistrations_TenantId_DeviceLibraryIdentifier",
                table: "DeviceRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_DeviceRegistrations_TenantId_SerialNumber",
                table: "DeviceRegistrations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CustomNotificationCampaigns_TenantId_Id",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_CreatedAt",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_ScheduledAtUtc",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_Status",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_CustomNotificationCampaigns_TenantId_Status_ScheduledAtUtc",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Customers_TenantId_Id",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_NormalizedPhone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_ReferredBy",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NormalizedUsername",
                table: "TenantAdminUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RewardCatalogItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProgramConfigs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PointCampaigns");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LoyaltyNotifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeviceRegistrations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomNotificationCampaigns");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_TenantAdminUsers_TenantId_Username",
                table: "TenantAdminUsers",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_IsActive",
                table: "RewardCatalogItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_IsMonthlyProduct",
                table: "RewardCatalogItems",
                column: "IsMonthlyProduct");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_LoyaltyCardId",
                table: "Redemptions",
                column: "LoyaltyCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_RewardCatalogItemId",
                table: "Redemptions",
                column: "RewardCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_Status",
                table: "Redemptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramConfigs_Key",
                table: "ProgramConfigs",
                column: "Key",
                unique: true);

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
                name: "IX_LoyaltyNotifications_CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                column: "CustomNotificationCampaignId");

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
                name: "IX_LoyaltyCards_CustomerId",
                table: "LoyaltyCards",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_SerialNumber",
                table: "DeviceRegistrations",
                column: "SerialNumber");

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

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ReferredBy",
                table: "Customers",
                column: "ReferredBy");

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyCards_Customers_CustomerId",
                table: "LoyaltyCards",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_CustomNotificationCampaigns_CustomNotificationCampaignId",
                table: "LoyaltyNotifications",
                column: "CustomNotificationCampaignId",
                principalTable: "CustomNotificationCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_Customers_CustomerId",
                table: "LoyaltyNotifications",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyNotifications_LoyaltyCards_LoyaltyCardId",
                table: "LoyaltyNotifications",
                column: "LoyaltyCardId",
                principalTable: "LoyaltyCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Redemptions_LoyaltyCards_LoyaltyCardId",
                table: "Redemptions",
                column: "LoyaltyCardId",
                principalTable: "LoyaltyCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Redemptions_RewardCatalogItems_RewardCatalogItemId",
                table: "Redemptions",
                column: "RewardCatalogItemId",
                principalTable: "RewardCatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
