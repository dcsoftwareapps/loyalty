using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStampRewardIdentityAndVisualAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RewardIdentityId",
                table: "StampRewardDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StampRewardIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageBlobName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampRewardIdentities", x => x.Id);
                    table.UniqueConstraint("AK_StampRewardIdentities_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_StampRewardIdentities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StampVisualConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmptyStampBlobName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    FilledStampBlobName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampVisualConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampVisualConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing definitions have no explicit continuity information. Give each
            // its own identity rather than guessing relationships across versions.
            migrationBuilder.Sql(@"
                INSERT INTO StampRewardIdentities (Id, TenantId)
                SELECT Id, TenantId FROM StampRewardDefinitions;
                UPDATE StampRewardDefinitions SET RewardIdentityId = Id;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "RewardIdentityId",
                table: "StampRewardDefinitions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampRewardDefinitions_TenantId_RewardIdentityId",
                table: "StampRewardDefinitions",
                columns: new[] { "TenantId", "RewardIdentityId" });

            migrationBuilder.CreateIndex(
                name: "IX_StampRewardDefinitions_TenantId_StampProgramVersionId_RewardIdentityId",
                table: "StampRewardDefinitions",
                columns: new[] { "TenantId", "StampProgramVersionId", "RewardIdentityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampVisualConfigurations_TenantId",
                table: "StampVisualConfigurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StampRewardDefinitions_StampRewardIdentities_TenantId_RewardIdentityId",
                table: "StampRewardDefinitions",
                columns: new[] { "TenantId", "RewardIdentityId" },
                principalTable: "StampRewardIdentities",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StampRewardDefinitions_StampRewardIdentities_TenantId_RewardIdentityId",
                table: "StampRewardDefinitions");

            migrationBuilder.DropTable(
                name: "StampRewardIdentities");

            migrationBuilder.DropTable(
                name: "StampVisualConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_StampRewardDefinitions_TenantId_RewardIdentityId",
                table: "StampRewardDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_StampRewardDefinitions_TenantId_StampProgramVersionId_RewardIdentityId",
                table: "StampRewardDefinitions");

            migrationBuilder.DropColumn(
                name: "RewardIdentityId",
                table: "StampRewardDefinitions");
        }
    }
}
