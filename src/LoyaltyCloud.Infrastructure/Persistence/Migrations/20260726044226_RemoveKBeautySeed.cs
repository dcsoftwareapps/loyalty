using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveKBeautySeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @TenantId uniqueidentifier = 'b1000000-0000-0000-0000-000000000001';

                DELETE FROM NotificationDeliveries WHERE TenantId = @TenantId;
                DELETE FROM LoyaltyNotifications WHERE TenantId = @TenantId;
                DELETE FROM PointLotConsumptions WHERE TenantId = @TenantId;
                DELETE FROM PointLots WHERE TenantId = @TenantId;
                DELETE FROM Redemptions WHERE TenantId = @TenantId;
                DELETE FROM PointTransactions WHERE TenantId = @TenantId;
                DELETE FROM DeviceRegistrations WHERE TenantId = @TenantId;
                DELETE FROM LoyaltyCards WHERE TenantId = @TenantId;
                DELETE FROM CustomNotificationCampaigns WHERE TenantId = @TenantId;
                DELETE FROM PointCampaigns WHERE TenantId = @TenantId;
                DELETE FROM RewardCatalogItems WHERE TenantId = @TenantId;
                DELETE FROM Customers WHERE TenantId = @TenantId;
                DELETE FROM ProgramConfigs WHERE TenantId = @TenantId;
                DELETE FROM TenantLoyaltyLevels WHERE TenantId = @TenantId;
                DELETE FROM TenantAdminUsers WHERE TenantId = @TenantId;
                DELETE FROM TenantBrandings WHERE TenantId = @TenantId;
                DELETE FROM TenantSubscriptions WHERE TenantId = @TenantId;
                DELETE FROM Tenants WHERE Id = @TenantId;
                """);

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "TenantBrandings",
                keyColumn: "TenantId",
                keyValue: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "TenantSubscriptions",
                keyColumn: "TenantId",
                keyValue: new Guid("b1000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("b1000000-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "IsActive", "Slug", "TimeZoneId", "UpdatedAt" },
                values: new object[] { new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "KBeauty", true, "kbeauty", "America/Tijuana", null });

            migrationBuilder.InsertData(
                table: "ProgramConfigs",
                columns: new[] { "Id", "Description", "Key", "TenantId", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "Pesos MXN por 1 punto (1 pt cada $10).", "points_per_peso_unit", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "10" },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "Puntos al registrarse.", "welcome_bonus_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "50" },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "Puntos por referido confirmado.", "referral_bonus_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "150" },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "Multiplicador en mes de cumpleaños.", "birthday_multiplier", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "2" },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), "Umbral inicio nivel Mist.", "level_mist_min", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "0" },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), "Umbral inicio nivel Glow.", "level_glow_min", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "1000" },
                    { new Guid("a1000000-0000-0000-0000-000000000007"), "Umbral inicio nivel Radiance.", "level_radiance_min", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "3000" },
                    { new Guid("a1000000-0000-0000-0000-000000000008"), "Puntos anuales para mantener Radiance.", "radiance_requalification_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "500" },
                    { new Guid("a1000000-0000-0000-0000-000000000009"), "Costo del mini producto.", "reward_mini_product_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "300" },
                    { new Guid("a1000000-0000-0000-0000-00000000000a"), "Costo del $50 off en compra.", "reward_fifty_off_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "500" },
                    { new Guid("a1000000-0000-0000-0000-00000000000b"), "Costo del análisis FocusSkin (Glow+).", "reward_focusskin_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "400" },
                    { new Guid("a1000000-0000-0000-0000-00000000000c"), "Costo del producto del mes (Glow+).", "reward_monthly_product_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "700" },
                    { new Guid("a1000000-0000-0000-0000-00000000000d"), "Costo del $100 off en cabina (Glow+).", "reward_hundred_off_cabina_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "800" },
                    { new Guid("a1000000-0000-0000-0000-00000000000e"), "Costo del $300 off en facial (Radiance).", "reward_facial_off_points", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "1200" },
                    { new Guid("a1000000-0000-0000-0000-00000000000f"), "Activa la expiracion automatica de puntos.", "points_expiration_enabled", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "true" },
                    { new Guid("a1000000-0000-0000-0000-000000000010"), "Meses de vigencia de cada lote de puntos.", "points_expire_after_months", new Guid("b1000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "12" }
                });

            migrationBuilder.InsertData(
                table: "TenantBrandings",
                columns: new[] { "TenantId", "InstagramUrl", "LogoUrl", "PrimaryColor", "SecondaryColor", "SupportPhone", "TermsUrl", "WhatsAppUrl" },
                values: new object[] { new Guid("b1000000-0000-0000-0000-000000000001"), null, null, "#1C1C1C", "#E8668E", null, null, null });

            migrationBuilder.InsertData(
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "CurrentPeriodEnd", "CurrentPeriodStart", "GracePeriodEndsAt", "LastPaymentAt", "PaidThroughUtc", "PlanCode", "Status", "SuspensionReason" },
                values: new object[] { new Guid("b1000000-0000-0000-0000-000000000001"), null, null, null, null, null, "internal", "Active", null });

            migrationBuilder.Sql(
                """
                DECLARE @TenantId uniqueidentifier = 'b1000000-0000-0000-0000-000000000001';

                INSERT INTO TenantLoyaltyLevels
                    (Id, TenantId, Name, NormalizedName, Threshold, SortOrder, IsActive, CreatedAt, UpdatedAt)
                SELECT
                    CONVERT(uniqueidentifier, levels.Id),
                    @TenantId,
                    levels.Name,
                    UPPER(levels.Name),
                    levels.Threshold,
                    levels.SortOrder,
                    CAST(1 AS bit),
                    '2026-07-01T00:00:00',
                    NULL
                FROM (VALUES
                    ('c1000000-0000-0000-0000-000000000001', N'Mist', 0, 1),
                    ('c1000000-0000-0000-0000-000000000002', N'Glow', 1000, 2),
                    ('c1000000-0000-0000-0000-000000000003', N'Radiance', 3000, 3)
                ) AS levels(Id, Name, Threshold, SortOrder)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM TenantLoyaltyLevels existing
                    WHERE existing.TenantId = @TenantId
                      AND existing.SortOrder = levels.SortOrder
                );
                """);
        }
    }
}
