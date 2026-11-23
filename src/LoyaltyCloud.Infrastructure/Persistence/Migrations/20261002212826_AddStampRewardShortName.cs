using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStampRewardShortName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "StampRewardDefinitions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "EarnedStampRewards",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
            // Backfill each historical snapshot from its own name, never the active definition.
            migrationBuilder.Sql("UPDATE StampRewardDefinitions SET ShortName = LEFT(LTRIM(RTRIM(Name)), 40)");
            migrationBuilder.Sql("UPDATE EarnedStampRewards SET ShortName = LEFT(LTRIM(RTRIM(Name)), 40)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "StampRewardDefinitions");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "EarnedStampRewards");
        }
    }
}
