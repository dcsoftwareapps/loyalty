using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberDigitalWalletsTenantized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberDigitalWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExternalClassId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalObjectId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    LastSynchronizedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastSynchronizationError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSaveLinkCreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberDigitalWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberDigitalWallets_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberDigitalWallets_LoyaltyCards_LoyaltyCardId",
                        column: x => x.LoyaltyCardId,
                        principalTable: "LoyaltyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberDigitalWallets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberDigitalWallets_CustomerId",
                table: "MemberDigitalWallets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberDigitalWallets_LoyaltyCardId",
                table: "MemberDigitalWallets",
                column: "LoyaltyCardId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberDigitalWallets_Provider_ExternalObjectId",
                table: "MemberDigitalWallets",
                columns: new[] { "Provider", "ExternalObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberDigitalWallets_TenantId_CustomerId_Provider",
                table: "MemberDigitalWallets",
                columns: new[] { "TenantId", "CustomerId", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberDigitalWallets_TenantId_LoyaltyCardId_Provider",
                table: "MemberDigitalWallets",
                columns: new[] { "TenantId", "LoyaltyCardId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberDigitalWallets_TenantId_Provider_Status",
                table: "MemberDigitalWallets",
                columns: new[] { "TenantId", "Provider", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberDigitalWallets");
        }
    }
}
