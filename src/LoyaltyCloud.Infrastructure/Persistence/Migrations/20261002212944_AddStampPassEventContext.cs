using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStampPassEventContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StampPassEventContexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRollover = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnnouncementEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampPassEventContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampPassEventContexts_LoyaltyCards_TenantId_SerialNumber",
                        columns: x => new { x.TenantId, x.SerialNumber },
                        principalTable: "LoyaltyCards",
                        principalColumns: new[] { "TenantId", "SerialNumber" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StampPassEventContexts_StampTransactions_TenantId_TransactionId",
                        columns: x => new { x.TenantId, x.TransactionId },
                        principalTable: "StampTransactions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StampPassEventContexts_TenantId_SerialNumber",
                table: "StampPassEventContexts",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampPassEventContexts_TenantId_TransactionId",
                table: "StampPassEventContexts",
                columns: new[] { "TenantId", "TransactionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StampPassEventContexts");
        }
    }
}
