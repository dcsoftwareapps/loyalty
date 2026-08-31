using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftCardAppleWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthenticationToken",
                table: "GiftCardWallets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_GiftCards_TenantId_Id",
                table: "GiftCards",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "GiftCardDeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiftCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceLibraryIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PassTypeIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PushToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCardDeviceRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftCardDeviceRegistrations_GiftCards_TenantId_GiftCardId",
                        columns: x => new { x.TenantId, x.GiftCardId },
                        principalTable: "GiftCards",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiftCardDeviceRegistrations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardDeviceRegistrations_DeviceLibraryIdentifier_PassTypeIdentifier_SerialNumber",
                table: "GiftCardDeviceRegistrations",
                columns: new[] { "DeviceLibraryIdentifier", "PassTypeIdentifier", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardDeviceRegistrations_TenantId_DeviceLibraryIdentifier",
                table: "GiftCardDeviceRegistrations",
                columns: new[] { "TenantId", "DeviceLibraryIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardDeviceRegistrations_TenantId_GiftCardId",
                table: "GiftCardDeviceRegistrations",
                columns: new[] { "TenantId", "GiftCardId" });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardDeviceRegistrations_TenantId_SerialNumber",
                table: "GiftCardDeviceRegistrations",
                columns: new[] { "TenantId", "SerialNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftCardDeviceRegistrations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_GiftCards_TenantId_Id",
                table: "GiftCards");

            migrationBuilder.DropColumn(
                name: "AuthenticationToken",
                table: "GiftCardWallets");
        }
    }
}
