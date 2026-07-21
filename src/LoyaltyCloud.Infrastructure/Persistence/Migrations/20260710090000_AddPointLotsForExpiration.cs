using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPointLotsForExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PointLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoyaltyCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePointTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalAmount = table.Column<int>(type: "int", nullable: false),
                    RemainingAmount = table.Column<int>(type: "int", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointLots_LoyaltyCards_LoyaltyCardId",
                        column: x => x.LoyaltyCardId,
                        principalTable: "LoyaltyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointLots_PointTransactions_SourcePointTransactionId",
                        column: x => x.SourcePointTransactionId,
                        principalTable: "PointTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointLotConsumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PointLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumingPointTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedemptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointLotConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointLotConsumptions_PointLots_PointLotId",
                        column: x => x.PointLotId,
                        principalTable: "PointLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointLotConsumptions_PointTransactions_ConsumingPointTransactionId",
                        column: x => x.ConsumingPointTransactionId,
                        principalTable: "PointTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointLotConsumptions_Redemptions_RedemptionId",
                        column: x => x.RedemptionId,
                        principalTable: "Redemptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_ConsumingPointTransactionId",
                table: "PointLotConsumptions",
                column: "ConsumingPointTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_PointLotId",
                table: "PointLotConsumptions",
                column: "PointLotId");

            migrationBuilder.CreateIndex(
                name: "IX_PointLotConsumptions_RedemptionId",
                table: "PointLotConsumptions",
                column: "RedemptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_ExpiresAt_RemainingAmount",
                table: "PointLots",
                columns: new[] { "ExpiresAt", "RemainingAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_LoyaltyCardId_ExpiresAt_EarnedAt",
                table: "PointLots",
                columns: new[] { "LoyaltyCardId", "ExpiresAt", "EarnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLots_SourcePointTransactionId",
                table: "PointLots",
                column: "SourcePointTransactionId",
                unique: true);

            migrationBuilder.InsertData(
                table: "ProgramConfigs",
                columns: new[] { "Id", "Description", "Key", "UpdatedAt", "UpdatedBy", "Value" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-00000000000f"), "Activa la expiracion automatica de puntos.", "points_expiration_enabled", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "true" },
                    { new Guid("a1000000-0000-0000-0000-000000000010"), "Meses de vigencia de cada lote de puntos.", "points_expire_after_months", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "12" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PointLotConsumptions");
            migrationBuilder.DropTable(name: "PointLots");

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "ProgramConfigs",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000010"));
        }
    }
}
