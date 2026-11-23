using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_TenantAdminUsers_TenantId_Id",
                table: "TenantAdminUsers",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "CashierRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierRefreshTokens_TenantAdminUsers_TenantId_TenantAdminUserId",
                        columns: x => new { x.TenantId, x.TenantAdminUserId },
                        principalTable: "TenantAdminUsers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashierRefreshTokens_TenantId_TenantAdminUserId",
                table: "CashierRefreshTokens",
                columns: new[] { "TenantId", "TenantAdminUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierRefreshTokens_TokenHash",
                table: "CashierRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashierRefreshTokens");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TenantAdminUsers_TenantId_Id",
                table: "TenantAdminUsers");
        }
    }
}
