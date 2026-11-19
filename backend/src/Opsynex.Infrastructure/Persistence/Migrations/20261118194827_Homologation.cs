using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opsynex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Homologation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountMappings_CompanyId_ClientAccount",
                table: "AccountMappings");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisStatus",
                table: "Uploads",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<int>(
                name: "DoubtfulAccounts",
                table: "Uploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "Uploads",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "Uploads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AccountMappings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Primary");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "AccountMappings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Statement");

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "AccountMappings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_CompanyId_Scope_Section_ClientAccount",
                table: "AccountMappings",
                columns: new[] { "CompanyId", "Scope", "Section", "ClientAccount" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountMappings_CompanyId_Scope_Section_ClientAccount",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "AnalysisStatus",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "DoubtfulAccounts",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "AccountMappings");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_CompanyId_ClientAccount",
                table: "AccountMappings",
                columns: new[] { "CompanyId", "ClientAccount" },
                unique: true);
        }
    }
}
