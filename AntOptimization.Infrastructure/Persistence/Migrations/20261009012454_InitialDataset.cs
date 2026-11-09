using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntOptimization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatasetJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    South = table.Column<double>(type: "REAL", nullable: false),
                    West = table.Column<double>(type: "REAL", nullable: false),
                    North = table.Column<double>(type: "REAL", nullable: false),
                    East = table.Column<double>(type: "REAL", nullable: false),
                    PointsPerInstance = table.Column<int>(type: "INTEGER", nullable: false),
                    InstanceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DistanceSource = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    FixedStart = table.Column<bool>(type: "INTEGER", nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false),
                    MasterSeed = table.Column<int>(type: "INTEGER", nullable: false),
                    GridJson = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigurationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRuns = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedRuns = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedInstances = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedInstances = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CoordinatesJson = table.Column<string>(type: "TEXT", nullable: true),
                    DistanceMatrixJson = table.Column<string>(type: "TEXT", nullable: true),
                    FixedStartIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    Features_PointCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Features_RegionWidthKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_RegionHeightKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_RegionAreaKm2 = table.Column<double>(type: "REAL", nullable: true),
                    Features_PointDensityPerKm2 = table.Column<double>(type: "REAL", nullable: true),
                    Features_CentroidDistanceMeanKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CentroidDistanceStdKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_SpatialNearestNeighborMeanKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_ClarkEvansRatio = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostMeanKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostStdKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostMinKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostMaxKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostMedianKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostCoefficientOfVariation = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostNearestNeighborMeanKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_CostNearestNeighborStdKm = table.Column<double>(type: "REAL", nullable: true),
                    Features_AsymmetryIndex = table.Column<double>(type: "REAL", nullable: true),
                    Features_DetourIndex = table.Column<double>(type: "REAL", nullable: true),
                    Features_NearestNeighborDistanceM = table.Column<double>(type: "REAL", nullable: true),
                    Features_TwoOptDistanceM = table.Column<double>(type: "REAL", nullable: true),
                    Features_NearestNeighborGapPercent = table.Column<double>(type: "REAL", nullable: true),
                    Features_TwoOptGapPercent = table.Column<double>(type: "REAL", nullable: true),
                    OptimalDistanceM = table.Column<double>(type: "REAL", nullable: true),
                    OptimalTourJson = table.Column<string>(type: "TEXT", nullable: true),
                    ExactMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    ExactTimeMs = table.Column<double>(type: "REAL", nullable: true),
                    ExactOperations = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatasetInstances_DatasetJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "DatasetJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcoRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Repetition = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Alpha = table.Column<double>(type: "REAL", nullable: false),
                    Beta = table.Column<double>(type: "REAL", nullable: false),
                    NumberOfAnts = table.Column<int>(type: "INTEGER", nullable: false),
                    EvaporationRate = table.Column<double>(type: "REAL", nullable: false),
                    Iterations = table.Column<int>(type: "INTEGER", nullable: false),
                    BestDistanceM = table.Column<double>(type: "REAL", nullable: false),
                    GapAbsM = table.Column<double>(type: "REAL", nullable: false),
                    GapPercent = table.Column<double>(type: "REAL", nullable: false),
                    ReachedOptimum = table.Column<bool>(type: "INTEGER", nullable: false),
                    BestIteration = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstOptimalIteration = table.Column<int>(type: "INTEGER", nullable: true),
                    ToursConstructed = table.Column<long>(type: "INTEGER", nullable: false),
                    ExecutionTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    BestTourJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcoRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcoRuns_DatasetInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "DatasetInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcoRuns_ConfigKey",
                table: "AcoRuns",
                column: "ConfigKey");

            migrationBuilder.CreateIndex(
                name: "IX_AcoRuns_InstanceId",
                table: "AcoRuns",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DatasetInstances_JobId_Index",
                table: "DatasetInstances",
                columns: new[] { "JobId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetJobs_CreatedAt",
                table: "DatasetJobs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcoRuns");

            migrationBuilder.DropTable(
                name: "DatasetInstances");

            migrationBuilder.DropTable(
                name: "DatasetJobs");
        }
    }
}
