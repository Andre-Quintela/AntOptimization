using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntOptimization.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A job now covers several instance sizes: <c>DatasetJobs.PointsPerInstance</c> is replaced by the
    /// list <c>PointCountsJson</c> plus <c>InstancesPerPointCount</c>, and each instance carries its own
    /// <c>PointCount</c>. Existing jobs are backfilled as single-size jobs before the old column is dropped.
    /// </summary>
    /// <inheritdoc />
    public partial class VariablePointCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PointCountsJson",
                table: "DatasetJobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "InstancesPerPointCount",
                table: "DatasetJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointCount",
                table: "DatasetInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill before dropping the source column: every existing job had a single size.
            migrationBuilder.Sql(@"
                UPDATE DatasetInstances
                SET PointCount = COALESCE((
                    SELECT j.PointsPerInstance FROM DatasetJobs j WHERE j.Id = DatasetInstances.JobId
                ), 0);");

            migrationBuilder.Sql(@"
                UPDATE DatasetJobs
                SET PointCountsJson = '[' || PointsPerInstance || ']',
                    InstancesPerPointCount = InstanceCount;");

            migrationBuilder.DropColumn(
                name: "PointsPerInstance",
                table: "DatasetJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsPerInstance",
                table: "DatasetJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Multi-size jobs cannot be represented by a single column; keep the smallest size.
            migrationBuilder.Sql(@"
                UPDATE DatasetJobs
                SET PointsPerInstance = COALESCE((
                    SELECT MIN(i.PointCount) FROM DatasetInstances i WHERE i.JobId = DatasetJobs.Id
                ), 0);");

            migrationBuilder.DropColumn(
                name: "PointCountsJson",
                table: "DatasetJobs");

            migrationBuilder.DropColumn(
                name: "InstancesPerPointCount",
                table: "DatasetJobs");

            migrationBuilder.DropColumn(
                name: "PointCount",
                table: "DatasetInstances");
        }
    }
}
