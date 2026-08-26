using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeOneMonthPriceId",
                table: "SubscriptionPlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSixMonthPriceId",
                table: "SubscriptionPlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeThreeMonthPriceId",
                table: "SubscriptionPlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeTwelveMonthPriceId",
                table: "SubscriptionPlans",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentKind",
                table: "BillingOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TenantBillingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutoRenewEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    BillingContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StripeSubscriptionStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    StripeCurrentPeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    RecurringMonths = table.Column<int>(type: "int", nullable: true),
                    RecurringAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RecurringCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CardBrand = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CardLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBillingProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("INSERT INTO [TenantBillingProfiles] ([Id], [TenantId], [AutoRenewEnabled], [CancelAtPeriodEnd]) SELECT NEWID(), [Id], 1, 0 FROM [Tenants];");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingProfiles_StripeCustomerId",
                table: "TenantBillingProfiles",
                column: "StripeCustomerId",
                unique: true,
                filter: "[StripeCustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingProfiles_StripeSubscriptionId",
                table: "TenantBillingProfiles",
                column: "StripeSubscriptionId",
                unique: true,
                filter: "[StripeSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingProfiles_TenantId",
                table: "TenantBillingProfiles",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantBillingProfiles");

            migrationBuilder.DropColumn(
                name: "StripeOneMonthPriceId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "StripeSixMonthPriceId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "StripeThreeMonthPriceId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "StripeTwelveMonthPriceId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "PaymentKind",
                table: "BillingOrders");
        }
    }
}
