using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opsynex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IngestionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RawAccountValues_UploadId",
                table: "RawAccountValues");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "Uploads",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Sheets",
                table: "Uploads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Uploads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Warnings",
                table: "Uploads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Years",
                table: "Uploads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountCode",
                table: "RawAccountValues",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedLabel",
                table: "RawAccountValues",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalLabel",
                table: "RawAccountValues",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RowKind",
                table: "RawAccountValues",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "RawAccountValues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "RawAccountValues",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SheetName",
                table: "RawAccountValues",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SheetType",
                table: "RawAccountValues",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AccountsPayable",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccountsReceivable",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cash",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentAssets",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLiabilities",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EarningsBeforeTax",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancialDebt",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancialExpenses",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossSales",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTax",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Inventory",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RawMaterials",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscounts",
                table: "FinancialData",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MonthlyFinancialData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Purchases = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyFinancialData", x => x.Id);
                    table.CheckConstraint("CK_MonthlyFinancialData_Month", "[Month] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_MonthlyFinancialData_Year", "[Year] BETWEEN 1900 AND 2200");
                    table.ForeignKey(
                        name: "FK_MonthlyFinancialData_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlyFinancialData_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Uploads_Sheets_Json",
                table: "Uploads",
                sql: "ISJSON([Sheets]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Uploads_Summary_Json",
                table: "Uploads",
                sql: "ISJSON([Summary]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Uploads_Warnings_Json",
                table: "Uploads",
                sql: "ISJSON([Warnings]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Uploads_Years_Json",
                table: "Uploads",
                sql: "ISJSON([Years]) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RawAccountValues_UploadId_SheetType",
                table: "RawAccountValues",
                columns: new[] { "UploadId", "SheetType" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFinancialData_CompanyId_Year_Month",
                table: "MonthlyFinancialData",
                columns: new[] { "CompanyId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFinancialData_UploadId",
                table: "MonthlyFinancialData",
                column: "UploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyFinancialData");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Uploads_Sheets_Json",
                table: "Uploads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Uploads_Summary_Json",
                table: "Uploads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Uploads_Warnings_Json",
                table: "Uploads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Uploads_Years_Json",
                table: "Uploads");

            migrationBuilder.DropIndex(
                name: "IX_RawAccountValues_UploadId_SheetType",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "Sheets",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "Warnings",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "Years",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "AccountCode",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "NormalizedLabel",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "OriginalLabel",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "RowKind",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "SheetName",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "SheetType",
                table: "RawAccountValues");

            migrationBuilder.DropColumn(
                name: "AccountsPayable",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "AccountsReceivable",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "Cash",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "CurrentAssets",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "CurrentLiabilities",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "EarningsBeforeTax",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "FinancialDebt",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "FinancialExpenses",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "GrossSales",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "IncomeTax",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "Inventory",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "RawMaterials",
                table: "FinancialData");

            migrationBuilder.DropColumn(
                name: "SalesDiscounts",
                table: "FinancialData");

            migrationBuilder.CreateIndex(
                name: "IX_RawAccountValues_UploadId",
                table: "RawAccountValues",
                column: "UploadId");
        }
    }
}
