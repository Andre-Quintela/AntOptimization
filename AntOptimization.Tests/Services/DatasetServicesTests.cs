using System.Globalization;
using System.Text;
using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.DTOs.Dataset;
using AntOptimization.Domain.Entities;
using AntOptimization.Domain.Exceptions;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Domain.Models;
using AntOptimization.Infrastructure.Persistence;
using AntOptimization.Services.Dataset;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AntOptimization.Tests.Services;

public class DatasetServicesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AntOptimizationDbContext _db;
    private readonly DatasetRepository _repository;
    private readonly Mock<IDatasetJobQueue> _queue = new();
    private readonly Mock<IDatasetCostMatrixProvider> _matrixProvider = new();
    private readonly DatasetOptions _options = new() { OsrmDelayMs = 0, MaxGenerationAttempts = 3 };

    public DatasetServicesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AntOptimizationDbContext(new DbContextOptionsBuilder<AntOptimizationDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        _repository = new DatasetRepository(_db);

        _matrixProvider
            .Setup(p => p.GetMatrixAsync(It.IsAny<List<Location>>(), It.IsAny<DistanceSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Location> l, DistanceSource _, CancellationToken _) => HaversineDistance.Matrix(l));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private DatasetGenerationService GenerationService => new(_repository, _queue.Object, _options);
    private DatasetJobRunner Runner => new(_repository, _matrixProvider.Object, _options);

    private static CreateDatasetJobRequest SmallRequest(int instances = 2, params int[] points) => new()
    {
        Name = "teste",
        Region = new RegionDto { South = -19.95, West = -43.97, North = -19.90, East = -43.92 },
        PointCounts = points.Length > 0 ? [.. points] : [6],
        InstancesPerPointCount = instances,
        DistanceSource = DistanceSource.Osrm,
        FixedStart = true,
        Repetitions = 2,
        MasterSeed = 1234,
        Grid = new HyperparameterGrid
        {
            Alpha = [1],
            Beta = [1, 3],
            NumberOfAnts = [5],
            EvaporationRate = [0.5],
            Iterations = [10]
        }
    };

    private async Task<Guid> CreateJobAsync(CreateDatasetJobRequest? request = null)
    {
        var (job, estimate) = await GenerationService.CreateJobAsync(request ?? SmallRequest());
        estimate.Errors.Should().BeEmpty();
        return job!.Id;
    }

    [Fact]
    public async Task CreateJob_PersistsPendingInstancesAndEnqueues()
    {
        var jobId = await CreateJobAsync();

        var job = await _repository.GetJobAsync(jobId);
        job!.Status.Should().Be(DatasetJobStatus.Pending);
        job.TotalRuns.Should().Be(2 * 2 * 2);
        (await _repository.GetInstancesAsync(jobId)).Should().HaveCount(2)
            .And.OnlyContain(i => i.Status == DatasetInstanceStatus.Pending);
        _queue.Verify(q => q.EnqueueAsync(jobId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Runner_CompletesJob_WithConsistentInstancesAndRuns()
    {
        var jobId = await CreateJobAsync();

        await Runner.RunAsync(jobId, CancellationToken.None);

        var job = await _repository.GetJobAsync(jobId);
        job!.Status.Should().Be(DatasetJobStatus.Completed);
        job.CompletedRuns.Should().Be(job.TotalRuns);
        job.FinishedAt.Should().NotBeNull();

        foreach (var instance in await _repository.GetInstancesAsync(jobId))
        {
            instance.Status.Should().Be(DatasetInstanceStatus.Completed);
            instance.Features.Should().NotBeNull();
            instance.Features!.PointCount.Should().Be(6);
            instance.OptimalDistanceM.Should().BeGreaterThan(0);
            instance.Features.NearestNeighborDistanceM.Should().BeGreaterThanOrEqualTo(instance.OptimalDistanceM!.Value - 1e-6);

            var runs = await _repository.GetRunsAsync(instance.Id);
            runs.Should().HaveCount(4);
            runs.Should().OnlyContain(r => r.Status == AcoRunStatus.Completed && r.GapPercent >= -1e-9);
            runs.Select(r => r.Seed).Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public async Task Runner_IsReproducible_ForSameMasterSeed()
    {
        var first = await CreateJobAsync();
        var second = await CreateJobAsync();

        await Runner.RunAsync(first, CancellationToken.None);
        await Runner.RunAsync(second, CancellationToken.None);

        var a = (await _repository.GetRunsForJobAsync(first)).OrderBy(r => r.Seed).Select(r => r.BestDistanceM);
        var b = (await _repository.GetRunsForJobAsync(second)).OrderBy(r => r.Seed).Select(r => r.BestDistanceM);
        a.Should().Equal(b);
    }

    [Fact]
    public async Task Runner_RegeneratesPoints_WhenLocationIsUnreachable()
    {
        int calls = 0;
        _matrixProvider
            .Setup(p => p.GetMatrixAsync(It.IsAny<List<Location>>(), It.IsAny<DistanceSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Location> l, DistanceSource _, CancellationToken _) =>
                ++calls == 1 ? throw new UnreachableLocationException(0, 1) : HaversineDistance.Matrix(l));
        var jobId = await CreateJobAsync(SmallRequest(instances: 1));

        await Runner.RunAsync(jobId, CancellationToken.None);

        var instance = (await _repository.GetInstancesAsync(jobId)).Single();
        instance.Status.Should().Be(DatasetInstanceStatus.Completed);
        instance.GenerationAttempts.Should().Be(2);
        instance.Seed.Should().Be(SeedDerivation.ForInstance(1234, 0, attempt: 1));
    }

    [Fact]
    public async Task Runner_RecordsFailures_AndResumeReprocessesThem()
    {
        _matrixProvider
            .Setup(p => p.GetMatrixAsync(It.IsAny<List<Location>>(), It.IsAny<DistanceSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        var jobId = await CreateJobAsync();

        await Runner.RunAsync(jobId, CancellationToken.None);

        var failed = await _repository.GetJobAsync(jobId);
        failed!.Status.Should().Be(DatasetJobStatus.Failed);
        failed.FailedInstances.Should().Be(2);
        (await _repository.GetInstancesAsync(jobId)).Should()
            .OnlyContain(i => i.Status == DatasetInstanceStatus.Failed && i.ErrorMessage!.Contains("offline"));

        // Network back: resume re-processes the failed instances.
        _matrixProvider
            .Setup(p => p.GetMatrixAsync(It.IsAny<List<Location>>(), It.IsAny<DistanceSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Location> l, DistanceSource _, CancellationToken _) => HaversineDistance.Matrix(l));
        (await GenerationService.ResumeAsync(jobId)).Should().Be(DatasetCommandResult.Ok);
        await Runner.RunAsync(jobId, CancellationToken.None);

        var resumed = await _repository.GetJobAsync(jobId);
        resumed!.Status.Should().Be(DatasetJobStatus.Completed);
        resumed.FailedInstances.Should().Be(0);
    }

    [Fact]
    public async Task Runner_MarksInstance_WhenExactLimitIsExceeded()
    {
        var jobId = await CreateJobAsync(SmallRequest(1, 7));
        var limitedRunner = new DatasetJobRunner(_repository, _matrixProvider.Object,
            new DatasetOptions { OsrmDelayMs = 0, MaxExactPoints = 6 });

        await limitedRunner.RunAsync(jobId, CancellationToken.None);

        var instance = (await _repository.GetInstancesAsync(jobId)).Single();
        instance.Status.Should().Be(DatasetInstanceStatus.ExceededLimit);
        instance.ErrorMessage.Should().Contain("limitado");
        (await _repository.GetRunsAsync(instance.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Runner_Cancelled_KeepsStatusCancelled()
    {
        var jobId = await CreateJobAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Runner.RunAsync(jobId, cts.Token);

        (await _repository.GetJobAsync(jobId))!.Status.Should().Be(DatasetJobStatus.Cancelled);
    }

    [Fact]
    public async Task ConfigSummaries_AggregateAllRunsPerConfiguration()
    {
        var jobId = await CreateJobAsync();
        await Runner.RunAsync(jobId, CancellationToken.None);

        var summaries = await GenerationService.GetConfigSummariesAsync(jobId);

        summaries.Should().HaveCount(2);
        summaries!.Should().OnlyContain(s => s.RunCount == 4 && s.InstanceCount == 2);
        summaries.Should().BeInAscendingOrder(s => s.MeanGapPercent);
        summaries.Sum(s => s.InstancesWon).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Export_RunsCsv_HasOneRowPerRun_InInvariantCulture()
    {
        var jobId = await CreateJobAsync();
        await Runner.RunAsync(jobId, CancellationToken.None);

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        string csv;
        try
        {
            using var stream = new MemoryStream();
            await new DatasetExportService(_repository).WriteAsync(jobId, DatasetExportLevel.Runs, stream);
            csv = Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1 + 8);
        var header = lines[0].TrimEnd('\r').Split(',');
        header.Should().Contain(["instance_id", "optimal_distance_m", "gap_percent", "alpha", "point_count", "clark_evans_ratio"]);
        // Duplicated names silently become point_count.1 in pandas; point_count is a key column only.
        header.Should().OnlyHaveUniqueItems();
        var row = lines[1].TrimEnd('\r').Split(',');
        row.Should().HaveCount(header.Length);
        double.Parse(row[Array.IndexOf(header, "optimal_distance_m")], CultureInfo.InvariantCulture).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Export_Zip_ContainsAllFiles()
    {
        var jobId = await CreateJobAsync(SmallRequest(instances: 1));
        await Runner.RunAsync(jobId, CancellationToken.None);

        using var stream = new MemoryStream();
        await new DatasetExportService(_repository).WriteAsync(jobId, DatasetExportLevel.Zip, stream);
        stream.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(stream);

        archive.Entries.Select(e => e.Name).Should()
            .BeEquivalentTo(["runs.csv", "instances.csv", "configs_summary.csv", "metadata.json"]);
    }

    [Fact]
    public async Task Delete_RemovesJobInstancesAndRuns()
    {
        var jobId = await CreateJobAsync(SmallRequest(instances: 1));
        await Runner.RunAsync(jobId, CancellationToken.None);

        (await GenerationService.DeleteAsync(jobId)).Should().Be(DatasetCommandResult.Ok);

        (await _repository.GetJobAsync(jobId)).Should().BeNull();
        (await _db.AcoRuns.CountAsync()).Should().Be(0);
        (await _db.DatasetInstances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Estimate_ReportsErrorsAndWarnings()
    {
        var invalid = SmallRequest();
        invalid.Region = new RegionDto();
        invalid.PointCounts = [30];
        invalid.Grid.EvaporationRate = [1.5];

        var estimate = GenerationService.Estimate(invalid);

        estimate.IsValid.Should().BeFalse();
        estimate.Errors.Should().HaveCount(3);

        var heldKarp = SmallRequest(2, 12);
        var ok = GenerationService.Estimate(heldKarp);
        ok.IsValid.Should().BeTrue();
        ok.ExactMethod.Should().Be("HeldKarp");
        ok.TotalRuns.Should().Be(8);
        ok.Warnings.Should().Contain(w => w.Contains("Held-Karp"));
    }

    [Fact]
    public void Estimate_RejectsJobsAboveRunLimit()
    {
        var service = new DatasetGenerationService(_repository, _queue.Object, new DatasetOptions { MaxRunsPerJob = 5 });

        service.Estimate(SmallRequest()).Errors.Should().Contain(e => e.Contains("excede o limite"));
    }

    [Fact]
    public async Task CreateJob_WithSeveralSizes_CreatesOneInstanceSetPerSizeAndReplica()
    {
        var request = SmallRequest(3, 5, 7);
        var jobId = await CreateJobAsync(request);

        var job = await _repository.GetJobAsync(jobId);
        job!.InstanceCount.Should().Be(6);                 // 2 sizes x 3 replicas
        job.InstancesPerPointCount.Should().Be(3);
        job.TotalRuns.Should().Be(6 * 2 * 2);              // instances x configs x repetitions

        var instances = await _repository.GetInstancesAsync(jobId);
        instances.Select(i => i.Index).Should().BeEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
        instances.Where(i => i.PointCount == 5).Should().HaveCount(3);
        instances.Where(i => i.PointCount == 7).Should().HaveCount(3);
        instances.Select(i => i.Seed).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Runner_GeneratesADistinctPointSetPerInstance_HonouringEachSize()
    {
        var jobId = await CreateJobAsync(SmallRequest(2, 3, 4));

        await Runner.RunAsync(jobId, CancellationToken.None);

        var job = await _repository.GetJobAsync(jobId);
        job!.Status.Should().Be(DatasetJobStatus.Completed);

        var instances = await _repository.GetInstancesAsync(jobId);
        instances.Should().HaveCount(4).And.OnlyContain(i => i.Status == DatasetInstanceStatus.Completed);

        foreach (var instance in instances)
        {
            var coordinates = System.Text.Json.JsonSerializer.Deserialize<List<AntOptimization.Domain.DTOs.LocationDto>>(instance.CoordinatesJson!)!;
            coordinates.Should().HaveCount(instance.PointCount);
            instance.Features!.PointCount.Should().Be(instance.PointCount);
        }

        // Every instance is an independent draw, so no two share the same coordinates.
        instances.Select(i => i.CoordinatesJson).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Estimate_NormalizesSizes_AndCountsTotalInstances()
    {
        var request = SmallRequest(4, 8, 6, 6);

        var estimate = GenerationService.Estimate(request);

        estimate.IsValid.Should().BeTrue();
        estimate.PointCounts.Should().BeEquivalentTo(new[] { 6, 8 }, o => o.WithStrictOrdering());
        estimate.TotalInstances.Should().Be(8);
        estimate.TotalRuns.Should().Be(8 * 2 * 2);
    }

    [Fact]
    public void Estimate_RejectsEmptyOrInfeasibleSizes()
    {
        var empty = SmallRequest();
        empty.PointCounts = [];
        GenerationService.Estimate(empty).Errors.Should().Contain(e => e.Contains("tamanho"));

        var tooSmall = SmallRequest(2, 1);
        GenerationService.Estimate(tooSmall).Errors.Should().Contain(e => e.Contains("2 pontos"));

        var tooBig = SmallRequest(2, 6, 30);
        GenerationService.Estimate(tooBig).Errors.Should().Contain(e => e.Contains("30 pontos"));
    }

    [Fact]
    public void Estimate_RejectsTooManyInstancesAcrossSizes()
    {
        var service = new DatasetGenerationService(_repository, _queue.Object, new DatasetOptions { MaxInstancesPerJob = 5 });

        var estimate = service.Estimate(SmallRequest(3, 5, 6, 7));

        estimate.IsValid.Should().BeFalse();
        estimate.TotalInstances.Should().Be(9);
        estimate.Errors.Should().Contain(e => e.Contains("total de inst"));
    }
}
