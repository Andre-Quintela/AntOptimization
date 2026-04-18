using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntOptimization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompareRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartLocationIndex = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompareRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptimizationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartLocationIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    BestRouteOrderJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalDistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    RouteCoordinatesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompareRunResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompareRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Algorithm = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BestRouteOrderJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalDistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    ExecutionTimeMs = table.Column<long>(type: "INTEGER", nullable: false),
                    RelativeGapPercent = table.Column<double>(type: "REAL", nullable: false),
                    RouteCoordinatesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompareRunResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompareRunResults_CompareRuns_CompareRunId",
                        column: x => x.CompareRunId,
                        principalTable: "CompareRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompareRunResults_CompareRunId",
                table: "CompareRunResults",
                column: "CompareRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CompareRuns_CreatedAtUtc",
                table: "CompareRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationRuns_CreatedAtUtc",
                table: "OptimizationRuns",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompareRunResults");

            migrationBuilder.DropTable(
                name: "OptimizationRuns");

            migrationBuilder.DropTable(
                name: "CompareRuns");
        }
    }
}
