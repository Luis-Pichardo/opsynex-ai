using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opsynex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KpiEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPerUnit",
                table: "KpiResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentRatio",
                table: "KpiResults",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DaysSalesOutstanding",
                table: "KpiResults",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DebtToEquity",
                table: "KpiResults",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyCostRatio",
                table: "KpiResults",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryDays",
                table: "KpiResults",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCostRatio",
                table: "KpiResults",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetMargin",
                table: "KpiResults",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Normalized",
                table: "KpiResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<decimal>(
                name: "OverheadRatio",
                table: "KpiResults",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trends",
                table: "KpiResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_Normalized_Json",
                table: "KpiResults",
                sql: "ISJSON([Normalized]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_Trends_Json",
                table: "KpiResults",
                sql: "ISJSON([Trends]) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_Normalized_Json",
                table: "KpiResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_Trends_Json",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "CostPerUnit",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "CurrentRatio",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "DaysSalesOutstanding",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "DebtToEquity",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "EnergyCostRatio",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "InventoryDays",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "LaborCostRatio",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "NetMargin",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "Normalized",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "OverheadRatio",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "Trends",
                table: "KpiResults");
        }
    }
}
