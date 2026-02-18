using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIImageAndVideoFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIProjects_Users_UserId",
                table: "AIProjects");

            migrationBuilder.DropIndex(
                name: "IX_AIProjects_UserId",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "FreeCredits",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "Month",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "Budget",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "DesignStyle",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "RoomType",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "Prompt",
                table: "AIDesigns");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "AIUsages",
                newName: "GenerationType");

            migrationBuilder.RenameColumn(
                name: "UsedCredits",
                table: "AIUsages",
                newName: "CreditsUsed");

            migrationBuilder.AddColumn<Guid>(
                name: "AIProjectId",
                table: "AIUsages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AIUsages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AIUsages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AIUsages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "AIUsages",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AIUsages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "AIUsages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AIUsages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AIUsages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextLabel",
                table: "AIProjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AIProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AIProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AIProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenerationType",
                table: "AIProjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AIProjects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NegativePrompt",
                table: "AIProjects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prompt",
                table: "AIProjects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceImageUrl",
                table: "AIProjects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AIProjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AIProjects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "AIDesigns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AIDesigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AIDesigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "AIDesigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "AIDesigns",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "AIDesigns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AIDesigns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OutputType",
                table: "AIDesigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OutputUrl",
                table: "AIDesigns",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderJobId",
                table: "AIDesigns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AIDesigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AIDesigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "AIDesigns",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AIProjectId",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AIUsages");

            migrationBuilder.DropColumn(
                name: "ContextLabel",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "GenerationType",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "NegativePrompt",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "Prompt",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "SourceImageUrl",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AIProjects");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "OutputType",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "OutputUrl",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "ProviderJobId",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AIDesigns");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "AIDesigns");

            migrationBuilder.RenameColumn(
                name: "GenerationType",
                table: "AIUsages",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "CreditsUsed",
                table: "AIUsages",
                newName: "UsedCredits");

            migrationBuilder.AddColumn<int>(
                name: "FreeCredits",
                table: "AIUsages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "AIUsages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Budget",
                table: "AIProjects",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DesignStyle",
                table: "AIProjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoomType",
                table: "AIProjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "AIProjects",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "AIDesigns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "AIDesigns",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "AIDesigns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Prompt",
                table: "AIDesigns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AIProjects_UserId",
                table: "AIProjects",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AIProjects_Users_UserId",
                table: "AIProjects",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
