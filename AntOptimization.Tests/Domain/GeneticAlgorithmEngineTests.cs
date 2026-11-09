using AntOptimization.Domain.Algorithms;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class GeneticAlgorithmEngineTests
{
    // Small parameters for fast tests
    private static GeneticAlgorithmParameters QuickParameters => new()
    {
        PopulationSize = 10,
        Generations = 20,
        MutationRate = 0.05,
        TournamentSize = 3
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
        var engine = new GeneticAlgorithmEngine(QuickParameters);

        var (bestTour, _) = engine.Solve(FourCityMatrix);

        bestTour.Should().HaveCount(4);
        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void Solve_WithFixedStartCity_TourBeginsAtCorrectCity()
    {
        var engine = new GeneticAlgorithmEngine(QuickParameters);

        var (bestTour, _) = engine.Solve(FourCityMatrix, fixedStartCity: 2);

        bestTour[0].Should().Be(2);
    }

    [Fact]
    public void Solve_ReturnsPositiveBestDistance()
    {
        var engine = new GeneticAlgorithmEngine(QuickParameters);

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
        var engine = new GeneticAlgorithmEngine(new GeneticAlgorithmParameters { PopulationSize = 5, Generations = 5 });

        var (bestTour, bestDistance) = engine.Solve(matrix, fixedStartCity: 0);

        bestTour.Should().Equal(new[] { 0, 1 });
        bestDistance.Should().Be(100);
    }

    [Fact]
    public void Solve_TourHasNoRepeatedCities()
    {
        var engine = new GeneticAlgorithmEngine(QuickParameters);

        var (bestTour, _) = engine.Solve(FourCityMatrix);

        bestTour.Should().OnlyHaveUniqueItems();
        bestTour.Should().HaveCount(FourCityMatrix.GetLength(0));
    }

    [Fact]
    public void Solve_WithSameSeed_IsReproducible()
    {
        var random = new Random(7);
        int n = 12;
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = i == j ? 0 : Math.Round(random.NextDouble() * 500 + 1, 3);

        var first = new GeneticAlgorithmEngine(QuickParameters, seed: 1234).Solve(matrix, fixedStartCity: 0);
        var second = new GeneticAlgorithmEngine(QuickParameters, seed: 1234).Solve(matrix, fixedStartCity: 0);

        second.BestTour.Should().Equal(first.BestTour);
        second.BestDistance.Should().Be(first.BestDistance);
    }

    [Fact]
    public void Solve_WithDifferentSeeds_ExploresDifferentSolutions()
    {
        var random = new Random(11);
        int n = 14;
        var matrix = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = i == j ? 0 : Math.Round(random.NextDouble() * 500 + 1, 3);

        var tours = Enumerable.Range(0, 5)
            .Select(seed => string.Join(',', new GeneticAlgorithmEngine(QuickParameters, seed).Solve(matrix, 0).BestTour))
            .ToList();

        tours.Distinct().Should().HaveCountGreaterThan(1);
    }
}
