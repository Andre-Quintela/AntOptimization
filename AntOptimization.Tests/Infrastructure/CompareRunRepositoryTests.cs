using AntOptimization.Domain.Entities;
using AntOptimization.Infrastructure.Persistence;
using AntOptimization.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntOptimization.Tests.Infrastructure;

public class CompareRunRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CompareRun BuildRun(int resultCount = 2) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAtUtc = DateTime.UtcNow,
        LocationCount = 4,
        StartLocationIndex = null,
        Results = Enumerable.Range(0, resultCount).Select(i => new CompareRunResult
        {
            Id = Guid.NewGuid(),
            Algorithm = $"Algo{i}",
            BestRouteOrderJson = "[0,1]",
            TotalDistanceKm = 10.0 + i,
            ExecutionTimeMs = 100L * i,
            RelativeGapPercent = i * 5.0,
            RouteCoordinatesJson = "[]"
        }).ToList()
    };

    [Fact]
    public async Task AddAsync_PersistsRunWithResults()
    {
        await using var context = CreateContext();
        var repo = new CompareRunRepository(context);
        var run = BuildRun(resultCount: 4);

        await repo.AddAsync(run);

        context.CompareRuns.Should().ContainSingle(r => r.Id == run.Id);
        context.CompareRunResults.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsRunsWithResultsOrderedDescending()
    {
        await using var context = CreateContext();
        var repo = new CompareRunRepository(context);

        var older = BuildRun();
        older.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);

        var newer = BuildRun();
        newer.CreatedAtUtc = DateTime.UtcNow;

        await repo.AddAsync(older);
        await repo.AddAsync(newer);

        var results = await repo.GetAllAsync();

        results.Should().HaveCount(2);
        results[0].Id.Should().Be(newer.Id);
        results[0].Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsRunWithResults()
    {
        await using var context = CreateContext();
        var repo = new CompareRunRepository(context);
        var run = BuildRun(resultCount: 3);

        await repo.AddAsync(run);

        var found = await repo.GetByIdAsync(run.Id);

        found.Should().NotBeNull();
        found!.Results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        await using var context = CreateContext();
        var repo = new CompareRunRepository(context);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
