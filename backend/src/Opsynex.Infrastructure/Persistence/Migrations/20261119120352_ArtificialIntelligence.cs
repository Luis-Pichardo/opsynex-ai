using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Opsynex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArtificialIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutiveSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Paragraphs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priorities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    InputHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutiveSummaries", x => x.Id);
                    table.CheckConstraint("CK_ExecutiveSummaries_Paragraphs_Json", "ISJSON([Paragraphs]) = 1");
                    table.CheckConstraint("CK_ExecutiveSummaries_Priorities_Json", "ISJSON([Priorities]) = 1");
                    table.CheckConstraint("CK_ExecutiveSummary_Year", "[Year] BETWEEN 1900 AND 2200");
                    table.ForeignKey(
                        name: "FK_ExecutiveSummaries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveSummaries_CompanyId_Year",
                table: "ExecutiveSummaries",
                columns: new[] { "CompanyId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutiveSummaries");
        }
    }
}
