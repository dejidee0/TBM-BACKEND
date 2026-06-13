using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorPortfolioProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BudgetMin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BudgetMax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DurationMinDays = table.Column<int>(type: "int", nullable: true),
                    DurationMaxDays = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScopeOfWork = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPortfolioProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioProjectImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageType = table.Column<int>(type: "int", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioProjectImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioProjectImages_VendorPortfolioProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "VendorPortfolioProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjectImages_ProjectId",
                table: "PortfolioProjectImages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPortfolioProjects_PublishedAt",
                table: "VendorPortfolioProjects",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPortfolioProjects_Status",
                table: "VendorPortfolioProjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPortfolioProjects_VendorId",
                table: "VendorPortfolioProjects",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioProjectImages");

            migrationBuilder.DropTable(
                name: "VendorPortfolioProjects");
        }
    }
}
