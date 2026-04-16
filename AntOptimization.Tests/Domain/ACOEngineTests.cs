using AntOptimization.Domain.Algorithms;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class ACOEngineTests
{
    // Small parameter set so tests run fast.
    private static ACOParameters QuickParameters => new()
    {
        NumberOfAnts = 5,
        EvaporationRate = 0.5,
        Alpha = 1.0,
        Beta = 2.0,
        Iterations = 10
    };

    private static double[,] FourCityMatrix => new double[,]
    {
        {   0, 100, 200, 150 },
        { 100,   0, 120, 180 },
        { 200, 120,   0,  90 },
        { 150, 180,  90,   0 }
    };

    [Fact]
    public void Solve_ReturnsValidTour_VisitsAllCities()
    {
        var engine = new ACOEngine(QuickParameters);

        var (bestTour, _) = engine.Solve(FourCityMatrix);

        bestTour.Should().HaveCount(4);
        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void Solve_WithFixedStartCity_TourBeginsAtCorrectCity()
    {
        var engine = new ACOEngine(QuickParameters);

        var (bestTour, _) = engine.Solve(FourCityMatrix, fixedStartCity: 2);

        bestTour[0].Should().Be(2);
    }

    [Fact]
    public void Solve_ReturnsPositiveBestDistance()
    {
        var engine = new ACOEngine(QuickParameters);

        var (_, bestDistance) = engine.Solve(FourCityMatrix);

        bestDistance.Should().BePositive();
    }

    [Fact]
    public void Solve_TwoCity_ReturnsCorrectDistance()
    {
        // With 2 cities and a fixed start, there is exactly one valid tour: [0, 1].
        var matrix = new double[,]
        {
            {   0, 100 },
            { 100,   0 }
        };
        var engine = new ACOEngine(new ACOParameters { NumberOfAnts = 2, Iterations = 5 });

        var (bestTour, bestDistance) = engine.Solve(matrix, fixedStartCity: 0);

        bestTour.Should().Equal(new[] { 0, 1 });
        bestDistance.Should().Be(100);
    }

    [Fact]
    public void SolveWithHistory_ReturnsCorrectNumberOfIterations()
    {
        var engine = new ACOEngine(QuickParameters); // Iterations = 10

        var (_, _, history) = engine.SolveWithHistory(FourCityMatrix);

        history.Should().HaveCount(10);
    }

    [Fact]
    public void SolveWithHistory_GlobalBestDistanceIsNonIncreasing()
    {
        var engine = new ACOEngine(QuickParameters);

        var (_, _, history) = engine.SolveWithHistory(FourCityMatrix);

        for (int i = 1; i < history.Count; i++)
            history[i].GlobalBestDistance.Should()
                .BeLessThanOrEqualTo(history[i - 1].GlobalBestDistance,
                    because: $"best distance should not worsen at iteration {i}");
    }

    [Fact]
    public void SolveWithHistory_EachSnapshotHasCorrectAntTourCount()
    {
        var engine = new ACOEngine(QuickParameters); // NumberOfAnts = 5

        var (_, _, history) = engine.SolveWithHistory(FourCityMatrix);

        history.Should().AllSatisfy(snapshot =>
            snapshot.AntTours.Should().HaveCount(QuickParameters.NumberOfAnts));
    }
}
