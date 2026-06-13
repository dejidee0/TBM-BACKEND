using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillsOfMaterials_DesignSessions_DesignSessionId",
                table: "BillsOfMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_DesignSessions_DesignSessionId",
                table: "Projects");

            migrationBuilder.CreateTable(
                name: "Discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApplicableTier = table.Column<int>(type: "int", nullable: true),
                    ApplicableBillingCycle = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: true),
                    CurrentRedemptions = table.Column<int>(type: "int", nullable: false),
                    SingleUsePerUser = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GenerationsPerMonth = table.Column<int>(type: "int", nullable: false),
                    PrioritySupport = table.Column<bool>(type: "bit", nullable: false),
                    UnlimitedGenerations = table.Column<bool>(type: "bit", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Features = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CanceledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaystackReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaystackSubscriptionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenerationsUsed = table.Column<int>(type: "int", nullable: false),
                    QuotaResetAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiscountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Discounts_DiscountId",
                        column: x => x.DiscountId,
                        principalTable: "Discounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DiscountId",
                table: "Subscriptions",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillsOfMaterials_DesignSessions_DesignSessionId",
                table: "BillsOfMaterials",
                column: "DesignSessionId",
                principalTable: "DesignSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_DesignSessions_DesignSessionId",
                table: "Projects",
                column: "DesignSessionId",
                principalTable: "DesignSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillsOfMaterials_DesignSessions_DesignSessionId",
                table: "BillsOfMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_DesignSessions_DesignSessionId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "PricingConfigs");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Discounts");

            migrationBuilder.AddForeignKey(
                name: "FK_BillsOfMaterials_DesignSessions_DesignSessionId",
                table: "BillsOfMaterials",
                column: "DesignSessionId",
                principalTable: "DesignSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_DesignSessions_DesignSessionId",
                table: "Projects",
                column: "DesignSessionId",
                principalTable: "DesignSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
