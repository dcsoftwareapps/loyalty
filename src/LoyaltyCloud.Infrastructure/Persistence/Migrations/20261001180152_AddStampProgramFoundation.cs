using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStampProgramFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StampProgramVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    MaxStamps = table.Column<int>(type: "int", nullable: false),
                    WelcomeStamps = table.Column<int>(type: "int", nullable: false),
                    ReversalPolicy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampProgramVersions", x => x.Id);
                    table.UniqueConstraint("AK_StampProgramVersions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StampProgramVersion_Rules", "[Version] > 0 AND [MaxStamps] > 0 AND [WelcomeStamps] >= 0 AND [WelcomeStamps] < [MaxStamps]");
                    table.ForeignKey(
                        name: "FK_StampProgramVersions_TenantAdminUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "TenantAdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StampProgramVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyCardSerialNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StampProgramVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    CurrentStamps = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampCycles", x => x.Id);
                    table.UniqueConstraint("AK_StampCycles_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StampCycle_Progress", "[Sequence] > 0 AND [CurrentStamps] >= 0");
                    table.ForeignKey(
                        name: "FK_StampCycles_LoyaltyCards_TenantId_LoyaltyCardSerialNumber",
                        columns: x => new { x.TenantId, x.LoyaltyCardSerialNumber },
                        principalTable: "LoyaltyCards",
                        principalColumns: new[] { "TenantId", "SerialNumber" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StampCycles_StampProgramVersions_TenantId_StampProgramVersionId",
                        columns: x => new { x.TenantId, x.StampProgramVersionId },
                        principalTable: "StampProgramVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampRewardDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StampProgramVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredStamps = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampRewardDefinitions", x => x.Id);
                    table.CheckConstraint("CK_StampRewardDefinition_Threshold", "[RequiredStamps] > 0");
                    table.ForeignKey(
                        name: "FK_StampRewardDefinitions_StampProgramVersions_TenantId_StampProgramVersionId",
                        columns: x => new { x.TenantId, x.StampProgramVersionId },
                        principalTable: "StampProgramVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StampCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    OperatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReversesTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampTransactions", x => x.Id);
                    table.UniqueConstraint("AK_StampTransactions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_StampTransaction_Delta", "[Delta] <> 0");
                    table.ForeignKey(
                        name: "FK_StampTransactions_StampCycles_TenantId_StampCycleId",
                        columns: x => new { x.TenantId, x.StampCycleId },
                        principalTable: "StampCycles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StampTransactions_StampTransactions_TenantId_ReversesTransactionId",
                        columns: x => new { x.TenantId, x.ReversesTransactionId },
                        principalTable: "StampTransactions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StampCycles_TenantId_LoyaltyCardSerialNumber",
                table: "StampCycles",
                columns: new[] { "TenantId", "LoyaltyCardSerialNumber" },
                unique: true,
                filter: "[CompletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StampCycles_TenantId_LoyaltyCardSerialNumber_Sequence",
                table: "StampCycles",
                columns: new[] { "TenantId", "LoyaltyCardSerialNumber", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampCycles_TenantId_StampProgramVersionId",
                table: "StampCycles",
                columns: new[] { "TenantId", "StampProgramVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StampProgramVersions_CreatedByUserId",
                table: "StampProgramVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StampProgramVersions_TenantId",
                table: "StampProgramVersions",
                column: "TenantId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StampProgramVersions_TenantId_Version",
                table: "StampProgramVersions",
                columns: new[] { "TenantId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampRewardDefinitions_TenantId_StampProgramVersionId_RequiredStamps",
                table: "StampRewardDefinitions",
                columns: new[] { "TenantId", "StampProgramVersionId", "RequiredStamps" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampTransactions_TenantId_ReversesTransactionId",
                table: "StampTransactions",
                columns: new[] { "TenantId", "ReversesTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StampTransactions_TenantId_StampCycleId_IdempotencyKey",
                table: "StampTransactions",
                columns: new[] { "TenantId", "StampCycleId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StampRewardDefinitions");

            migrationBuilder.DropTable(
                name: "StampTransactions");

            migrationBuilder.DropTable(
                name: "StampCycles");

            migrationBuilder.DropTable(
                name: "StampProgramVersions");
        }
    }
}
