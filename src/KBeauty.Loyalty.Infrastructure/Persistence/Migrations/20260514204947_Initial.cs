using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KBeauty.Loyalty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: false),
                    ReferredBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceLibraryIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PassTypeIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PushToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PointsCost = table.Column<int>(type: "int", nullable: false),
                    MinLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsMonthlyProduct = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentPoints = table.Column<int>(type: "int", nullable: false),
                    LifetimePoints = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PointsEarnedThisYear = table.Column<int>(type: "int", nullable: false),
                    LevelAchievedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AuthenticationToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyCards_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BonusType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointTransactions_LoyaltyCards_LoyaltyCardId",
                        column: x => x.LoyaltyCardId,
                        principalTable: "LoyaltyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PointsSpent = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Redemptions_LoyaltyCards_LoyaltyCardId",
                        column: x => x.LoyaltyCardId,
                        principalTable: "LoyaltyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Redemptions_RewardCatalogItems_RewardCatalogItemId",
                        column: x => x.RewardCatalogItemId,
                        principalTable: "RewardCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ProgramConfigs",
                columns: new[] { "Id", "Description", "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "Pesos MXN por 1 punto (1 pt cada $10).", "points_per_peso_unit", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "10" },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "Puntos al registrarse.", "welcome_bonus_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "50" },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "Puntos por referido confirmado.", "referral_bonus_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "150" },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "Multiplicador en mes de cumpleaños.", "birthday_multiplier", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "2" },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), "Umbral inicio nivel Mist.", "level_mist_min", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "0" },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), "Umbral inicio nivel Glow.", "level_glow_min", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "1000" },
                    { new Guid("a1000000-0000-0000-0000-000000000007"), "Umbral inicio nivel Radiance.", "level_radiance_min", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "3000" },
                    { new Guid("a1000000-0000-0000-0000-000000000008"), "Puntos anuales para mantener Radiance.", "radiance_requalification_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "500" },
                    { new Guid("a1000000-0000-0000-0000-000000000009"), "Costo del mini producto.", "reward_mini_product_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "300" },
                    { new Guid("a1000000-0000-0000-0000-00000000000a"), "Costo del $50 off en compra.", "reward_fifty_off_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "500" },
                    { new Guid("a1000000-0000-0000-0000-00000000000b"), "Costo del análisis FocusSkin (Glow+).", "reward_focusskin_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "400" },
                    { new Guid("a1000000-0000-0000-0000-00000000000c"), "Costo del producto del mes (Glow+).", "reward_monthly_product_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "700" },
                    { new Guid("a1000000-0000-0000-0000-00000000000d"), "Costo del $100 off en cabina (Glow+).", "reward_hundred_off_cabina_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "800" },
                    { new Guid("a1000000-0000-0000-0000-00000000000e"), "Costo del $300 off en facial (Radiance).", "reward_facial_off_points", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "1200" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ReferredBy",
                table: "Customers",
                column: "ReferredBy");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_DeviceLibraryIdentifier_PassTypeIdentifier_SerialNumber",
                table: "DeviceRegistrations",
                columns: new[] { "DeviceLibraryIdentifier", "PassTypeIdentifier", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_SerialNumber",
                table: "DeviceRegistrations",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_CustomerId",
                table: "LoyaltyCards",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_SerialNumber",
                table: "LoyaltyCards",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_LoyaltyCardId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "LoyaltyCardId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramConfigs_Key",
                table: "ProgramConfigs",
                column: "Key",
                unique: true);

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
                name: "IX_RewardCatalogItems_IsActive",
                table: "RewardCatalogItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RewardCatalogItems_IsMonthlyProduct",
                table: "RewardCatalogItems",
                column: "IsMonthlyProduct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceRegistrations");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "ProgramConfigs");

            migrationBuilder.DropTable(
                name: "Redemptions");

            migrationBuilder.DropTable(
                name: "LoyaltyCards");

            migrationBuilder.DropTable(
                name: "RewardCatalogItems");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
