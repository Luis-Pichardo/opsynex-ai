using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opsynex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Diagnosis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Highlights",
                table: "KpiResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_Highlights_Json",
                table: "KpiResults",
                sql: "ISJSON([Highlights]) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_Highlights_Json",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "Highlights",
                table: "KpiResults");
        }
    }
}
