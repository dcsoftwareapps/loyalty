using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStampOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StampTransactions_TenantId_ReversesTransactionId",
                table: "StampTransactions");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "StampTransactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "EarnedStampRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StampCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEarnTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StampRewardDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiredStamps = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarnedStampRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EarnedStampRewards_StampCycles_TenantId_StampCycleId",
                        columns: x => new { x.TenantId, x.StampCycleId },
                        principalTable: "StampCycles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EarnedStampRewards_StampTransactions_TenantId_SourceEarnTransactionId",
                        columns: x => new { x.TenantId, x.SourceEarnTransactionId },
                        principalTable: "StampTransactions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampOperationReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Operation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RewardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampOperationReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampOperationReceipts_LoyaltyCards_TenantId_SerialNumber",
                        columns: x => new { x.TenantId, x.SerialNumber },
                        principalTable: "LoyaltyCards",
                        principalColumns: new[] { "TenantId", "SerialNumber" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StampTransactions_TenantId_ReversesTransactionId",
                table: "StampTransactions",
                columns: new[] { "TenantId", "ReversesTransactionId" },
                unique: true,
                filter: "[ReversesTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StampTransactions_TenantId_StampCycleId_Sequence",
                table: "StampTransactions",
                columns: new[] { "TenantId", "StampCycleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarnedStampRewards_TenantId_SourceEarnTransactionId_RequiredStamps",
                table: "EarnedStampRewards",
                columns: new[] { "TenantId", "SourceEarnTransactionId", "RequiredStamps" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarnedStampRewards_TenantId_StampCycleId_Status",
                table: "EarnedStampRewards",
                columns: new[] { "TenantId", "StampCycleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StampOperationReceipts_TenantId_IdempotencyKey",
                table: "StampOperationReceipts",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampOperationReceipts_TenantId_SerialNumber",
                table: "StampOperationReceipts",
                columns: new[] { "TenantId", "SerialNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EarnedStampRewards");

            migrationBuilder.DropTable(
                name: "StampOperationReceipts");

            migrationBuilder.DropIndex(
                name: "IX_StampTransactions_TenantId_ReversesTransactionId",
                table: "StampTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StampTransactions_TenantId_StampCycleId_Sequence",
                table: "StampTransactions");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "StampTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_StampTransactions_TenantId_ReversesTransactionId",
                table: "StampTransactions",
                columns: new[] { "TenantId", "ReversesTransactionId" });
        }
    }
}
