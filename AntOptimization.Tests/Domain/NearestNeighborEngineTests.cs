using AntOptimization.Domain.Algorithms;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class NearestNeighborEngineTests
{
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
        var engine = new NearestNeighborEngine();

        var (bestTour, _) = engine.Solve(FourCityMatrix);

        bestTour.Should().HaveCount(4);
        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void Solve_WithFixedStartCity_TourBeginsAtCorrectCity()
    {
        var engine = new NearestNeighborEngine();

        var (bestTour, _) = engine.Solve(FourCityMatrix, fixedStartCity: 2);

        bestTour[0].Should().Be(2);
    }

    [Fact]
    public void Solve_ReturnsPositiveBestDistance()
    {
        var engine = new NearestNeighborEngine();

        var (_, bestDistance) = engine.Solve(FourCityMatrix);

        bestDistance.Should().BePositive();
    }

    [Fact]
    public void Solve_TwoCity_ReturnsCorrectDistance()
    {
        var matrix = new double[,]
        {
            {   0, 100 },
            { 100,   0 }
        };
        var engine = new NearestNeighborEngine();

        var (bestTour, bestDistance) = engine.Solve(matrix, fixedStartCity: 0);

        bestTour.Should().Equal(new[] { 0, 1 });
        bestDistance.Should().Be(100);
    }

    [Fact]
    public void Solve_TourHasNoRepeatedCities()
    {
        var engine = new NearestNeighborEngine();

        var (bestTour, _) = engine.Solve(FourCityMatrix);

        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().HaveCount(FourCityMatrix.GetLength(0));
    }
}
