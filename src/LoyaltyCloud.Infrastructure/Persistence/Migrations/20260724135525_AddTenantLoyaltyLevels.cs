using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLoyaltyLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantLoyaltyLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLoyaltyLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantLoyaltyLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLoyaltyLevels_TenantId_IsActive_SortOrder",
                table: "TenantLoyaltyLevels",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLoyaltyLevels_TenantId_NormalizedName",
                table: "TenantLoyaltyLevels",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantLoyaltyLevels_TenantId_SortOrder",
                table: "TenantLoyaltyLevels",
                columns: new[] { "TenantId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantLoyaltyLevels_TenantId_Threshold",
                table: "TenantLoyaltyLevels",
                columns: new[] { "TenantId", "Threshold" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO TenantLoyaltyLevels
                    (Id, TenantId, Name, NormalizedName, Threshold, SortOrder, IsActive, CreatedAt, UpdatedAt)
                SELECT
                    NEWID(),
                    t.Id,
                    levels.Name,
                    UPPER(levels.Name),
                    levels.Threshold,
                    levels.SortOrder,
                    CAST(1 AS bit),
                    SYSUTCDATETIME(),
                    NULL
                FROM Tenants t
                CROSS APPLY (VALUES
                    (
                        N'Mist',
                        COALESCE((
                            SELECT TOP (1) TRY_CONVERT(int, pc.Value)
                            FROM ProgramConfigs pc
                            WHERE pc.TenantId = t.Id AND pc.[Key] = N'level_mist_min'
                        ), 0),
                        1
                    ),
                    (
                        N'Glow',
                        COALESCE((
                            SELECT TOP (1) TRY_CONVERT(int, pc.Value)
                            FROM ProgramConfigs pc
                            WHERE pc.TenantId = t.Id AND pc.[Key] = N'level_glow_min'
                        ), 1000),
                        2
                    ),
                    (
                        N'Radiance',
                        COALESCE((
                            SELECT TOP (1) TRY_CONVERT(int, pc.Value)
                            FROM ProgramConfigs pc
                            WHERE pc.TenantId = t.Id AND pc.[Key] = N'level_radiance_min'
                        ), 3000),
                        3
                    )
                ) AS levels(Name, Threshold, SortOrder)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM TenantLoyaltyLevels existing
                    WHERE existing.TenantId = t.Id
                      AND existing.SortOrder = levels.SortOrder
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantLoyaltyLevels");
        }
    }
}
