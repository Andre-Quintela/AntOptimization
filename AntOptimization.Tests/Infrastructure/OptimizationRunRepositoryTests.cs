using AntOptimization.Domain.Entities;
using AntOptimization.Infrastructure.Persistence;
using AntOptimization.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntOptimization.Tests.Infrastructure;

public class OptimizationRunRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static OptimizationRun BuildRun(double distanceKm = 12.5) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAtUtc = DateTime.UtcNow,
        LocationCount = 3,
        StartLocationIndex = null,
        BestRouteOrderJson = "[0,2,1]",
        TotalDistanceKm = distanceKm,
        RouteCoordinatesJson = "[]"
    };

    [Fact]
    public async Task AddAsync_PersistsRun()
    {
        await using var context = CreateContext();
        var repo = new OptimizationRunRepository(context);
        var run = BuildRun();

        await repo.AddAsync(run);

        context.OptimizationRuns.Should().ContainSingle(r => r.Id == run.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsRunsOrderedByCreatedAtDescending()
    {
        await using var context = CreateContext();
        var repo = new OptimizationRunRepository(context);

        var older = BuildRun();
        older.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10);

        var newer = BuildRun();
        newer.CreatedAtUtc = DateTime.UtcNow;

        await repo.AddAsync(older);
        await repo.AddAsync(newer);

        var results = await repo.GetAllAsync();

        results.Should().HaveCount(2);
        results[0].Id.Should().Be(newer.Id);
        results[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectRun()
    {
        await using var context = CreateContext();
        var repo = new OptimizationRunRepository(context);
        var run = BuildRun(distanceKm: 7.3);

        await repo.AddAsync(run);

        var found = await repo.GetByIdAsync(run.Id);

        found.Should().NotBeNull();
        found!.TotalDistanceKm.Should().Be(7.3);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        await using var context = CreateContext();
        var repo = new OptimizationRunRepository(context);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
